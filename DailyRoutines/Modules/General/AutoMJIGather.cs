using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using DailyRoutines.Helpers;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using DailyRoutines.Notifications;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Memory;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.MJI;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;
using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoMJIGatherTitle", "AutoMJIGatherDescription", ModuleCategories.一般)]
public class AutoMJIGather : DailyModuleBase
{
    private class Config : ModuleConfiguration
    {
        public readonly List<IslandGatherPoint> IslandGatherPoints = [];
        public bool StopWhenReachingCap = true;
    }

    private class IslandGatherPoint : IEquatable<IslandGatherPoint>, IComparable<IslandGatherPoint>
    {
        public string Name { get; set; } = string.Empty;
        public bool Enabled { get; set; }
        public List<Vector3> Points { get; set; } = [];

        public IslandGatherPoint() { }

        public IslandGatherPoint(string name) => Name = name;

        public override bool Equals(object? obj) => Equals(obj as IslandGatherPoint);

        public bool Equals(IslandGatherPoint? other) => other != null && Name == other.Name;

        public override int GetHashCode() => Name.GetHashCode();

        public int CompareTo(IslandGatherPoint? other) => other == null ? 1 : string.Compare(Name, other.Name, StringComparison.Ordinal);
    }

    private static int CurrentGatherIndex;
    private static bool IsOnDataCollecting;
    private static List<Vector3> QueuedGatheringList = [];

    private delegate bool IsPlayerOnDivingDelegate(nint a1);
    [Signature("E8 ?? ?? ?? ?? 84 C0 74 ?? F3 0F 10 35 ?? ?? ?? ?? F3 0F 10 3D ?? ?? ?? ?? F3 44 0F 10 05", 
               DetourName = nameof(IsPlayingOnDivingDetour))]
    private static Hook<IsPlayerOnDivingDelegate>? IsPlayerOnDivingHook;

    [Signature("4C 8D 35 ?? ?? ?? ?? 48 8B 09", ScanType = ScanType.Text)]
    private static nint UnknownPtrInTargetSystem;

    private static bool IsOnDiving;

    private static Config ModuleConfig = null!;

    public override void Init()
    {
        ModuleConfig = LoadConfig<Config>() ?? new();

        Service.Hook.InitializeFromAttributes(this);
        IsPlayerOnDivingHook.Enable();

        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 15000, ShowDebug = false };

        Service.Chat.ChatMessage += OnChatMessage;
        Service.FrameworkManager.Register(OnUpdate);
    }

    public override void ConfigUI()
    {
        ImGui.BeginDisabled(Service.ClientState.TerritoryType != 1055 || TaskManager.IsBusy);

        ImGui.BeginDisabled(IsOnDataCollecting);
        if (ImGui.Button(Service.Lang.GetText("Start")))
        {
            TaskManager.Enqueue(SwitchToGatherMode);
            QueuedGatheringList = ModuleConfig.IslandGatherPoints
                                              .Where(group => group.Enabled)
                                              .SelectMany(group => group.Points ?? Enumerable.Empty<Vector3>())
                                              .ToList();

            if (!IsPlayerOnDivingHook.IsEnabled) IsPlayerOnDivingHook.Enable();

            if (QueuedGatheringList.Count != 0 && QueuedGatheringList.Count > 10) Gather(QueuedGatheringList);
            else Service.Chat.PrintError(Service.Lang.GetText("AutoMJIGather-InsufficientGatherNodes"));
        }

        ImGui.EndDisabled();
        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("Stop")))
        {
            TaskManager.Abort();
            IsOnDiving = false;
        }

        ImGui.SameLine();
        ImGui.Text(Service.Lang.GetText("AutoMJIGather-GatherProcessInfo",
                                        QueuedGatheringList.Count == 0 ? 0 : CurrentGatherIndex + 1,
                                        QueuedGatheringList.Count));

        ImGui.SameLine();
        if (ImGui.Checkbox(Service.Lang.GetText("AutoMJIGather-StopWhenReachCaps"), ref ModuleConfig.StopWhenReachingCap))
            SaveConfig(ModuleConfig);

        ImGui.Spacing();
        
        var tableSize = (ImGui.GetContentRegionAvail() / 3 + ImGuiHelpers.ScaledVector2(50f)) with { Y = 0 };
        if (ImGui.BeginTable("GatherPointsTable1", 3, ImGuiTableFlags.Borders, tableSize))
        {
            ImGui.TableSetupColumn("启用", ImGuiTableColumnFlags.WidthFixed, Styles.CheckboxSize.X);
            ImGui.TableSetupColumn("名称", ImGuiTableColumnFlags.None, 40);
            ImGui.TableSetupColumn("数量", ImGuiTableColumnFlags.None, 20);

            ImGui.TableNextRow(ImGuiTableRowFlags.Headers);

            ImGui.TableNextColumn();
            if (IsOnDataCollecting) ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.HealerGreen);
            if (ImGuiOm.SelectableIconCentered("DataCollect", FontAwesomeIcon.Play))
            {
                IsOnDataCollecting ^= true;
                if (!IsOnDataCollecting) SaveConfig(ModuleConfig);
            }
            if (IsOnDataCollecting) ImGui.PopStyleColor();
            ImGuiOm.TooltipHover(Service.Lang.GetText("AutoMJIGather-CollectGatherPointsHelp"));

            ImGui.TableNextColumn();
            ImGuiOm.Text(Service.Lang.GetText("Name"));

            ImGui.TableNextColumn();
            ImGuiOm.Text(Service.Lang.GetText("Amount"));

            foreach (var gatherPoint in ModuleConfig.IslandGatherPoints.Take(ModuleConfig.IslandGatherPoints.Count / 2))
            {
                if (gatherPoint.Enabled) ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudYellow);
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                var isEnabled = gatherPoint.Enabled;
                if (ImGui.Checkbox($"###{gatherPoint.Name}", ref isEnabled))
                    gatherPoint.Enabled = isEnabled;

                ImGui.TableNextColumn();
                ImGui.Text(gatherPoint.Name);

                ImGui.TableNextColumn();
                ImGui.Text(gatherPoint.Points.Count.ToString());
                if (gatherPoint.Enabled) ImGui.PopStyleColor();
            }

            ImGui.EndTable();
        }

        ImGui.SameLine();
        if (ImGui.BeginTable("GatherPointsTable2", 3, ImGuiTableFlags.Borders, tableSize))
        {
            ImGui.TableSetupColumn("启用", ImGuiTableColumnFlags.WidthFixed, Styles.CheckboxSize.X);
            ImGui.TableSetupColumn("名称", ImGuiTableColumnFlags.None, 40);
            ImGui.TableSetupColumn("数量", ImGuiTableColumnFlags.None, 20);

            ImGui.TableNextRow(ImGuiTableRowFlags.Headers);

            ImGui.TableNextColumn();
            if (IsOnDataCollecting) ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.HealerGreen);
            if (ImGuiOm.SelectableIconCentered("DataCollect", FontAwesomeIcon.Play))
            {
                IsOnDataCollecting ^= true;
                if (!IsOnDataCollecting) SaveConfig(ModuleConfig);
            }
            if (IsOnDataCollecting) ImGui.PopStyleColor();
            ImGuiOm.TooltipHover(Service.Lang.GetText("AutoMJIGather-CollectGatherPointsHelp"));

            ImGui.TableNextColumn();
            ImGuiOm.Text(Service.Lang.GetText("Name"));

            ImGui.TableNextColumn();
            ImGuiOm.Text(Service.Lang.GetText("Amount"));

            foreach (var gatherPoint in ModuleConfig.IslandGatherPoints.Skip(ModuleConfig.IslandGatherPoints.Count / 2))
            {
                if (gatherPoint.Enabled) ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudYellow);
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                var isEnabled = gatherPoint.Enabled;
                if (ImGui.Checkbox($"###{gatherPoint.Name}", ref isEnabled))
                    gatherPoint.Enabled = isEnabled;

                ImGui.TableNextColumn();
                ImGui.Text(gatherPoint.Name);

                ImGui.TableNextColumn();
                ImGui.Text(gatherPoint.Points.Count.ToString());
                if (gatherPoint.Enabled) ImGui.PopStyleColor();
            }

            ImGui.EndTable();
        }
    }

    private bool? Gather(IReadOnlyList<Vector3> nodes)
    {
        if (IsOccupied()) return false;

        TaskManager.Enqueue(SwitchToGatherMode);
        TaskManager.Enqueue(() => { IsOnDiving = nodes[CurrentGatherIndex].Y < 0; });
        TaskManager.Enqueue(() =>
        {
            var node = nodes[CurrentGatherIndex];
            return node.Y switch
            {
                < 0 when !IsPlayerOnDivingHook.Original(Service.ClientState.LocalPlayer.Address + 528) => Teleport(node),
                > 0 when IsPlayerOnDivingHook.Original(Service.ClientState.LocalPlayer.Address + 528) => TeleportWhenDiving(node),
                _ => node.Y > 0 ? Teleport(node) : TeleportWhenDiving(node)
            };
        });

        TaskManager.DelayNext(100);
        TaskManager.Enqueue(() => InteractWithNearestObject(nodes[CurrentGatherIndex]));

        TaskManager.DelayNext(100);
        TaskManager.Enqueue(() => Gather(QueuedGatheringList));

        TaskManager.DelayNext(100);
        if (CurrentGatherIndex + 1 >= nodes.Count)
        {
            TaskManager.Abort();
            TaskManager.Enqueue(() => CurrentGatherIndex = 0);
            TaskManager.Enqueue(() => Gather(QueuedGatheringList));
        }
        else TaskManager.Enqueue(() => CurrentGatherIndex++);

        return true;
    }

    private static unsafe bool? SwitchToGatherMode()
    {
        AgentHelper.SendEvent(AgentId.MJIHud, 2, 0, 1, 82042U, 0U, 0);
        return MJIManager.Instance()->CurrentMode == 1;
    }

    private bool? Teleport(Vector3 pos)
    {
        if (IsOnGathering()) return false;
        if (Service.ClientState.TerritoryType != 1055)
        {
            TaskManager.Abort();
            WinToast.Notify("", Service.Lang.GetText("AutoMJIGather-NotInIslandMessage"), ToolTipIcon.Warning);
            return true;
        }

        var localPlayer = Service.ClientState.LocalPlayer;
        if (localPlayer == null) return false;

        var address = localPlayer.Address + 176;
        MemoryHelper.Write(address, pos.X);
        MemoryHelper.Write(address + 4, pos.Y);
        MemoryHelper.Write(address + 8, pos.Z);

        return true;

    }

    private bool? TeleportWhenDiving(Vector3 pos)
    {
        if (IsOnGathering()) return false;
        if (Service.ClientState.TerritoryType != 1055)
        {
            TaskManager.Abort();
            WinToast.Notify("", Service.Lang.GetText("AutoMJIGather-NotInIslandMessage"), ToolTipIcon.Warning);
            return true;
        }

        var ptr = UnknownPtrInTargetSystem;
        var address = Marshal.ReadInt32(ptr + 3) + ptr + 22151;

        MemoryHelper.Write(address, pos.X);
        MemoryHelper.Write(address + 4, pos.Y);
        MemoryHelper.Write(address + 8, pos.Z);
        return true;
    }

    private unsafe bool? InteractWithNearestObject(Vector3 node)
    {
        if (IsOccupied()) return false;

        var nearObjects = Service.ObjectTable
                                 .Where(x => x.ObjectKind is ObjectKind.CardStand &&
                                             Vector3.Distance(x.Position, Service.ClientState.LocalPlayer.Position) <= 2)
                                 .ToArray();

        if (nearObjects.Length == 0)
        {
            Service.Log.Warning("没有找到采集点, 正在重新定位坐标");
            _ = node.Y > 0 ? Teleport(node with { Y = node.Y - 1 }) : TeleportWhenDiving(node with { Y = node.Y - 1 });
            return false;
        }

        if (IsOnGathering()) return false;

        TargetSystem.Instance()->InteractWithObject((GameObject*)nearObjects.FirstOrDefault().Address);
        return true;
    }

    private static bool IsOnGathering()
    {
        return Service.Condition[ConditionFlag.Jumping] ||
               Service.Condition[ConditionFlag.Jumping61] ||
               Service.Condition[ConditionFlag.OccupiedInQuestEvent] ||
               Service.Condition[ConditionFlag.Casting] ||
               Service.Condition[ConditionFlag.BetweenAreas];
    }

    private static bool IsPlayingOnDivingDetour(nint a1)
    {
        var original = IsPlayerOnDivingHook.Original(a1);
        if (Service.ClientState.TerritoryType != 1055)
        {
            IsPlayerOnDivingHook.Disable();
            return original;
        }

        return IsOnDiving;
    }

    private static void OnUpdate(IFramework _)
    {
        if (!IsOnDataCollecting || Service.ClientState.TerritoryType != 1055) return;

        foreach (var obj in Service.ObjectTable)
        {
            // 排除非采集点和特定耕地
            if (obj.ObjectKind != ObjectKind.CardStand || 
                obj.DataId == 0 || obj.DataId == 2013159) continue;

            if (string.IsNullOrWhiteSpace(obj.Name.TextValue)) continue;

            var newGatherPoint = new IslandGatherPoint(obj.Name.TextValue);
            if (!ModuleConfig.IslandGatherPoints.Contains(newGatherPoint))
            {
                ModuleConfig.IslandGatherPoints.Add(newGatherPoint);
                newGatherPoint.Points.Add(obj.Position);
            }
            else
            {
                var existingPoint = ModuleConfig.IslandGatherPoints.Find(p => p.Equals(newGatherPoint));
                if (!existingPoint.Points.Contains(obj.Position))
                    existingPoint.Points.Add(obj.Position);
            }
        }
    }

    private void OnChatMessage(
        XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if (!ModuleConfig.StopWhenReachingCap) return;
        if (!TaskManager.IsBusy || (ushort)type != 2108) return;

        if (message.TextValue.Contains("持有数量已达到上限"))
        {
            TaskManager.Abort();
            IsPlayerOnDivingHook.Disable();
            WinToast.Notify("", Service.Lang.GetText("AutoMJIGather-ReachCapsMessage"), ToolTipIcon.Warning);
        }
    }

    public override void Uninit()
    {
        Service.FrameworkManager.Unregister(OnUpdate);
        Service.Chat.ChatMessage -= OnChatMessage;
        QueuedGatheringList.Clear();
        IsOnDataCollecting = false;
        CurrentGatherIndex = 0;

        base.Uninit();
    }
}
