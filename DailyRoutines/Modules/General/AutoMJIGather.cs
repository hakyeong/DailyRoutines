using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Memory;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.MJI;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoMJIGatherTitle", "AutoMJIGatherDescription", ModuleCategories.General)]
public class AutoMJIGather : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithUI => true;

    private static TaskManager? TaskManager;

    private static Dictionary<string, AutoMJIGatherGroup> GatherNodes = [];
    private static Thread? DataCollectThread;
    private static int CurrentGatherIndex;
    private static bool IsDataCollectThreadRunning = true;
    private static bool IsOnDataCollecting;
    private static bool IsOnGathering;
    private static List<Vector3> QueuedGatheringList = [];

    public void Init()
    {
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 10000, ShowDebug = false };
        DataCollectThread ??= new Thread(CollectGatheringPointData);
        switch (DataCollectThread.ThreadState)
        {
            case ThreadState.Unstarted:
                DataCollectThread.Start();
                break;
            case ThreadState.Stopped:
                DataCollectThread = new Thread(CollectGatheringPointData);
                DataCollectThread.Start();
                break;
        }

        if (!Service.Config.ConfigExists(typeof(AutoMJIGather), "GatherNodes"))
            Service.Config.AddConfig(typeof(AutoMJIGather), "GatherNodes", GatherNodes);

        GatherNodes =
            Service.Config.GetConfig<Dictionary<string, AutoMJIGatherGroup>>(typeof(AutoMJIGather), "GatherNodes");

        Initialized = true;
    }

    public void UI()
    {
        ImGui.BeginDisabled(Service.ClientState.TerritoryType != 1055 || IsOnGathering);
        ImGui.SetNextItemWidth(420f);
        var gatherNodes = GatherNodes;
        if (ImGui.BeginCombo("##AutoMJIGather-GatherNodes", Service.Lang.GetText("AutoMJIGather-NodesInfo", gatherNodes.Count, gatherNodes.Count(x => x.Value.Enabled), GatherNodes.Values.Where(group => group.Enabled).SelectMany(group => group.Nodes).Count()), ImGuiComboFlags.HeightLarge))
        {
            if (ImGui.Button(Service.Lang.GetText("AutoMJIGather-CollectGatherPointsInfo", IsOnDataCollecting ? Service.Lang.GetText("AutoMJIGather-Stop") : Service.Lang.GetText("AutoMJIGather-Start")))) IsOnDataCollecting = !IsOnDataCollecting;

            ImGuiOm.HelpMarker(Service.Lang.GetText("AutoMJIGather-CollectGatherPointsHelp"));

            ImGui.Separator();

            if (GatherNodes.Any())
            {
                ImGui.BeginGroup();
                foreach (var nodeGroup in GatherNodes)
                {
                    var groupState = nodeGroup.Value.Enabled;
                    if (ImGui.Checkbox($"##{nodeGroup.Key}", ref groupState))
                    {
                        GatherNodes[nodeGroup.Key] = new AutoMJIGatherGroup(groupState, nodeGroup.Value.Nodes);
                        Service.Config.UpdateConfig(typeof(AutoMJIGather), "GatherNodes", GatherNodes);
                        CurrentGatherIndex = 0;
                        QueuedGatheringList.Clear();
                    }
                }

                ImGui.EndGroup();

                ImGui.SameLine();
                ImGui.BeginGroup();
                foreach (var nodeGroup in GatherNodes)
                {
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text($"{nodeGroup.Key}");
                }

                ImGui.EndGroup();

                ImGui.SameLine();
                ImGui.Spacing();

                ImGui.SameLine();
                ImGui.BeginGroup();
                foreach (var nodeGroup in GatherNodes)
                {
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text($"{nodeGroup.Value.Nodes.Count}");
                }

                ImGui.EndGroup();
            }

            ImGui.EndCombo();
        }

        ImGui.SameLine();
        ImGui.BeginDisabled(IsOnDataCollecting || !GatherNodes.Any());
        if (ImGui.Button(Service.Lang.GetText("AutoMJIGather-Start")))
        {
            TaskManager.Enqueue(SwitchToGatherMode);
            QueuedGatheringList = GatherNodes.Values
                                             .Where(group => group.Enabled)
                                             .SelectMany(group => group.Nodes ?? Enumerable.Empty<Vector3>())
                                             .ToList();

            if (QueuedGatheringList.Any() && QueuedGatheringList.Count > 20)
            {
                IsOnGathering = true;
                Gather(QueuedGatheringList);
            }
        }
        ImGui.EndDisabled();
        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("AutoMJIGather-Stop")))
        {
            IsOnGathering = false;
            TaskManager.Abort();
        }

        ImGui.SameLine();
        ImGui.Text(Service.Lang.GetText("AutoMJIGather-GatherProcessInfo", CurrentGatherIndex, QueuedGatheringList.Count));
    }

    private static void CollectGatheringPointData()
    {
        while (IsDataCollectThreadRunning)
            if (IsOnDataCollecting)
            {
                foreach (var obj in Service.ObjectTable)
                {
                    if (obj.ObjectKind != ObjectKind.CardStand) continue;

                    var objName = obj.Name.ExtractText();
                    if (objName.Contains("海岛") || string.IsNullOrWhiteSpace(objName)) continue;
                    if (!GatherNodes.ContainsKey(objName)) GatherNodes.Add(objName, new AutoMJIGatherGroup(false, []));
                    if (GatherNodes[objName].Nodes.Add(obj.Position))
                        Service.Config.UpdateConfig(typeof(AutoMJIGather), "GatherNodes", GatherNodes);
                }
            }
    }

    private static bool? Gather(IReadOnlyList<Vector3> nodes)
    {
        if (IsOccupied()) return false;
        if (CurrentGatherIndex >= nodes.Count - 1) CurrentGatherIndex = 0;

        TaskManager.Enqueue(() => Teleport(nodes[CurrentGatherIndex]));
        TaskManager.DelayNext(500);
        TaskManager.Enqueue(() => InteractWithNearestObject(nodes[CurrentGatherIndex]));
        TaskManager.DelayNext(2000);
        CurrentGatherIndex++;
        TaskManager.Enqueue(() => Gather(QueuedGatheringList));
        return true;
    }

    private static unsafe bool? SwitchToGatherMode()
    {
        if (MJIManager.Instance()->CurrentMode == 1) return true;

        if (TryGetAddonByName<AtkUnitBase>("MJIHud", out var hud) && HelpersOm.IsAddonAndNodesReady(hud))
        {
            Callback.Fire(hud, true, 11, 0);
            if (TryGetAddonByName<AtkUnitBase>("ContextIconMenu", out var menu) && HelpersOm.IsAddonAndNodesReady(menu))
            {
                Callback.Fire(menu, true, 0, 1, 82043, 0, 0);
                TaskManager.Enqueue(CloseContextIconMenu);
                return true;
            }
        }

        return false;
    }

    private static unsafe bool? CloseContextIconMenu()
    {
        if (TryGetAddonByName<AtkUnitBase>("ContextIconMenu", out var menu) && HelpersOm.IsAddonAndNodesReady(menu))
        {
            Callback.Fire(menu, true, -1);
            menu->Close(true);
            return true;
        }

        return true;
    }

    private static bool? Teleport(Vector3 pos)
    {
        if (IsOccupied()) return false;

        if (Service.ClientState.LocalPlayer != null)
        {
            var address = Service.ClientState.LocalPlayer.Address;
            MemoryHelper.Write(address + 176, pos.X);
            MemoryHelper.Write(address + 180, pos.Y);
            MemoryHelper.Write(address + 184, pos.Z);

            return true;
        }

        return false;
    }

    private static unsafe bool? InteractWithNearestObject(Vector3 node)
    {
        if (IsOccupied()) return false;

        var nearObjects = Service.ObjectTable
                                 .Where(x => x.ObjectKind is ObjectKind.CardStand &&
                                             CalculateDistance(Service.ClientState.LocalPlayer.Position, x.Position) <=
                                             2).ToList();
        if (!nearObjects.Any())
        {
            Service.Log.Warning("没有找到采集点, 正在重新定位坐标"); ;
            Teleport(node with { Y = node.Y - 1 });
            return false;
        }

        if (Service.Condition[ConditionFlag.Jumping] || Service.Condition[ConditionFlag.Jumping61] ||
            IsOccupied()) return false;

        TargetSystem.Instance()->InteractWithObject((GameObject*)nearObjects.FirstOrDefault().Address);

        return true;
    }

    private static float CalculateDistance(Vector3 vector1, Vector3 vector2)
    {
        var deltaX = vector1.X - vector2.X;
        var deltaY = vector1.Y - vector2.Y;
        var deltaZ = vector1.Z - vector2.Z;

        return (float)Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY) + (deltaZ * deltaZ));
    }

    public void Uninit()
    {
        Service.Config.UpdateConfig(typeof(AutoMJIGather), "GatherNodes", GatherNodes);
        QueuedGatheringList.Clear();
        IsOnGathering = IsDataCollectThreadRunning = IsOnDataCollecting = false;
        CurrentGatherIndex = 0;
        TaskManager?.Abort();
        Service.Config.Save();

        Initialized = false;
    }
}

public class AutoMJIGatherGroup
{
    public bool Enabled { get; set; }
    public HashSet<Vector3>? Nodes { get; set; }

    public AutoMJIGatherGroup() { }

    public AutoMJIGatherGroup(bool enabled, HashSet<Vector3> nodes)
    {
        Enabled = enabled;
        Nodes = nodes;
    }
}
