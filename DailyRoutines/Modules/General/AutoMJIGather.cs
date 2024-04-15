using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Windows.Forms;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Utility;
using Dalamud.Memory;
using Dalamud.Plugin.Services;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.MJI;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoMJIGatherTitle", "AutoMJIGatherDescription", ModuleCategories.General)]
public class AutoMJIGather : DailyModuleBase
{
    #region StaticStatisitics
    private class AutoMJIGatherGroup
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

    private static readonly HashSet<Vector3> FarmCorpsPos =
    [
        // 第一行
        new Vector3(-240, 57.9f, 101),
        new Vector3(-240, 57.9f, 106.5f),
        new Vector3(-240, 57.9f, 112),
        new Vector3(-240, 57.9f, 117.5f),
        new Vector3(-240, 57.9f, 123),
        new Vector3(-240, 57.9f, 101),
        // 第二行
        new Vector3(-216, 60.4f, 97),
        new Vector3(-216, 60.4f, 102.5f),
        new Vector3(-216, 60.4f, 108),
        new Vector3(-216, 60.4f, 113.5f),
        new Vector3(-216, 60.4f, 119),
        // 第三行
        new Vector3(-189, 66.4f, 105),
        new Vector3(-189, 66.4f, 110.5f),
        new Vector3(-189, 66.4f, 116),
        new Vector3(-189, 66.4f, 121.5f),
        new Vector3(-189, 66.4f, 127),
        // 第四行
        new Vector3(-179, 66.4f, 105),
        new Vector3(-179, 66.4f, 110.5f),
        new Vector3(-179, 66.4f, 116),
        new Vector3(-179, 66.4f, 121.5f),
        new Vector3(-179, 66.4f, 127)
    ];

    #endregion

    private static Dictionary<string, AutoMJIGatherGroup> GatherNodes = [];
    private static bool ConfigStopWhenReachCaps;
    private static int CurrentGatherIndex;
    private static bool IsOnDataCollecting;
    private static List<Vector3> QueuedGatheringList = [];

    public override void Init()
    {
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 10000, ShowDebug = false };

        AddConfig(this, "GatherNodes", GatherNodes);
        AddConfig(this, "StopWhenReachCaps", true);

        GatherNodes =
            GetConfig<Dictionary<string, AutoMJIGatherGroup>>(this, "GatherNodes");
        ConfigStopWhenReachCaps = GetConfig<bool>(this, "StopWhenReachCaps");

        Service.Chat.ChatMessage += OnChatMessage;
    }

    public override void ConfigUI()
    {
        ImGui.BeginDisabled(Service.ClientState.TerritoryType != 1055 || TaskManager.IsBusy);
        ImGui.SetNextItemWidth(350f * ImGuiHelpers.GlobalScale);
        if (ImGui.BeginCombo("##AutoMJIGather-GatherNodes",
                             Service.Lang.GetText("AutoMJIGather-NodesInfo", GatherNodes.Count,
                                                  GatherNodes.Count(x => x.Value.Enabled),
                                                  GatherNodes.Values.Where(group => group.Enabled)
                                                             .SelectMany(group => group.Nodes).Count()),
                             ImGuiComboFlags.HeightLarge))
        {
            if (ImGui.Button(Service.Lang.GetText("AutoMJIGather-CollectGatherPointsInfo",
                                                  IsOnDataCollecting
                                                      ? Service.Lang.GetText("Stop")
                                                      : Service.Lang.GetText("Start"))))
            {
                if (IsOnDataCollecting)
                {
                    Service.Framework.Update -= OnUpdate;
                    IsOnDataCollecting = false;
                }
                else
                {
                    var keysToRemove = GatherNodes
                                       .Where(pair => pair.Value.Nodes == null ||
                                                      pair.Value.Nodes.Any(node => node.Y < 0))
                                       .Select(pair => pair.Key)
                                       .ToList();

                    foreach (var key in keysToRemove) GatherNodes.Remove(key);
                    Service.Framework.Update += OnUpdate;
                    IsOnDataCollecting = true;
                }
            }

            ImGuiOm.HelpMarker(Service.Lang.GetText("AutoMJIGather-CollectGatherPointsHelp"));

            ImGui.Separator();

            if (GatherNodes.Count != 0)
            {
                ImGui.BeginGroup();
                foreach (var nodeGroup in GatherNodes)
                {
                    var groupState = nodeGroup.Value.Enabled;
                    if (ImGui.Checkbox($"##{nodeGroup.Key}", ref groupState))
                    {
                        GatherNodes[nodeGroup.Key] = new AutoMJIGatherGroup(groupState, nodeGroup.Value.Nodes);
                        UpdateConfig(this, "GatherNodes", GatherNodes);
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
        ImGui.BeginDisabled(IsOnDataCollecting || GatherNodes.Count == 0);
        if (ImGui.Button(Service.Lang.GetText("Start")))
        {
            TaskManager.Enqueue(SwitchToGatherMode);
            QueuedGatheringList = GatherNodes.Values
                                             .Where(group => group.Enabled)
                                             .SelectMany(group => group.Nodes ?? Enumerable.Empty<Vector3>())
                                             .ToList();

            if (QueuedGatheringList.Count != 0 && QueuedGatheringList.Count > 10)
            {
                Gather(QueuedGatheringList);
            }
            else
                Service.Chat.PrintError(Service.Lang.GetText("AutoMJIGather-InsufficientGatherNodes"));
        }

        ImGui.EndDisabled();
        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("Stop")))
        {
            TaskManager.Abort();
        }

        ImGui.Text(Service.Lang.GetText("AutoMJIGather-GatherProcessInfo",
                                        QueuedGatheringList.Count == 0 ? 0 : CurrentGatherIndex + 1,
                                        QueuedGatheringList.Count));

        ImGui.Spacing();

        if (ImGui.Checkbox(Service.Lang.GetText("AutoMJIGather-StopWhenReachCaps"), ref ConfigStopWhenReachCaps))
            UpdateConfig(this, "StopWhenReachCaps", ConfigStopWhenReachCaps);
    }

    private void OnUpdate(IFramework framework)
    {
        if (!IsOnDataCollecting)
        {
            Service.Framework.Update -= OnUpdate;
            return;
        }

        foreach (var obj in Service.ObjectTable)
        {
            if (obj.Position.Y < 0 || obj.ObjectKind != ObjectKind.CardStand ||
                FarmCorpsPos.Contains(obj.Position)) continue;

            var objName = obj.Name.ExtractText();
            if (string.IsNullOrWhiteSpace(objName)) continue;
            if (!GatherNodes.ContainsKey(objName)) GatherNodes.Add(objName, new AutoMJIGatherGroup(false, []));
            if (GatherNodes[objName].Nodes.Add(obj.Position))
                UpdateConfig(this, "GatherNodes", GatherNodes);
        }
    }

    private void OnChatMessage(
        XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if (!ConfigStopWhenReachCaps) return;
        if (!TaskManager.IsBusy || (ushort)type != 2108) return;

        if (message.ExtractText().Contains("持有数量已达到上限"))
        {
            TaskManager.Abort();
            WinToast.Notify("", Service.Lang.GetText("AutoMJIGather-ReachCapsMessage"), ToolTipIcon.Warning);
        }
    }

    private bool? Gather(IReadOnlyList<Vector3> nodes)
    {
        if (IsOccupied()) return false;

        TaskManager.Enqueue(() => Teleport(nodes[CurrentGatherIndex]));
        TaskManager.DelayNext(500);
        TaskManager.Enqueue(() => InteractWithNearestObject(nodes[CurrentGatherIndex]));
        TaskManager.DelayNext(2000);
        TaskManager.Enqueue(() => Gather(QueuedGatheringList));
        if (CurrentGatherIndex + 1 >= nodes.Count)
        {
            TaskManager.Abort();
            TaskManager.Enqueue(() => CurrentGatherIndex = 0);
            TaskManager.Enqueue(() => Gather(QueuedGatheringList));
        }
        else
            TaskManager.Enqueue(() => CurrentGatherIndex++);

        return true;
    }

    private unsafe bool? SwitchToGatherMode()
    {
        if (MJIManager.Instance()->CurrentMode == 1) return true;

        if (TryGetAddonByName<AtkUnitBase>("MJIHud", out var hud) && HelpersOm.IsAddonAndNodesReady(hud))
        {
            AddonManager.Callback(hud, true, 11, 0);
            if (TryGetAddonByName<AtkUnitBase>("ContextIconMenu", out var menu) && HelpersOm.IsAddonAndNodesReady(menu))
            {
                AddonManager.Callback(menu, true, 0, 1, 82043, 0, 0);
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
            AddonManager.Callback(menu, true, -1);
            menu->Close(true);
            return true;
        }

        return true;
    }

    private bool? Teleport(Vector3 pos)
    {
        if (IsGathering()) return false;
        if (Service.ClientState.TerritoryType != 1055)
        {
            TaskManager.Abort();
            WinToast.Notify("", Service.Lang.GetText("AutoMJIGather-NotInIslandMessage"), ToolTipIcon.Warning);
            return true;
        }

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

    private unsafe bool? InteractWithNearestObject(Vector3 node)
    {
        if (IsOccupied()) return false;

        var nearObjects = Service.ObjectTable
                                 .Where(x => x.ObjectKind is ObjectKind.CardStand &&
                                             HelpersOm.GetGameDistanceFromObject(
                                                 (GameObject*)Service.ClientState.LocalPlayer.Address,
                                                 (GameObject*)x.Address) <= 2).ToArray();
        if (!nearObjects.Any())
        {
            Service.Log.Warning("没有找到采集点, 正在重新定位坐标");
            Teleport(node with { Y = node.Y - 1 });
            return false;
        }

        if (IsGathering()) return false;

        TargetSystem.Instance()->InteractWithObject((GameObject*)nearObjects.FirstOrDefault().Address);

        return true;
    }

    private static bool IsGathering()
    {
        return Service.Condition[ConditionFlag.Jumping] ||
               Service.Condition[ConditionFlag.Jumping61] ||
               Service.Condition[ConditionFlag.OccupiedInQuestEvent];
    }

    public override void Uninit()
    {
        UpdateConfig(this, "GatherNodes", GatherNodes);

        Service.Framework.Update -= OnUpdate;
        Service.Chat.ChatMessage -= OnChatMessage;
        QueuedGatheringList.Clear();
        IsOnDataCollecting = false;
        CurrentGatherIndex = 0;

        base.Uninit();
    }
}
