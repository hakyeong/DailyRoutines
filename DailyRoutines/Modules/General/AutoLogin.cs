using System;
using System.Collections.Generic;
using System.Linq;
using ClickLib;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using ECommons.Automation;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoLoginTitle", "AutoLoginDescription", ModuleCategories.General)]
public class AutoLogin : DailyModuleBase
{
    private class LoginInfo(uint worldID, int index) : IEquatable<LoginInfo>
    {
        public uint WorldID { get; set; } = worldID;
        public int CharaIndex { get; set; } = index;

        public override bool Equals(object? obj)
        {
            return Equals(obj as LoginInfo);
        }

        public bool Equals(LoginInfo? other)
        {
            if (other is null || GetType() != other.GetType())
                return false;

            return WorldID == other.WorldID && CharaIndex == other.CharaIndex;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(WorldID, CharaIndex);
        }

        public static bool operator ==(LoginInfo? lhs, LoginInfo? rhs)
        {
            if (lhs is null)
            {
                return rhs is null;
            }
            return lhs.Equals(rhs);
        }

        public static bool operator !=(LoginInfo lhs, LoginInfo rhs)
        {
            return !(lhs == rhs);
        }
    }

    private enum BehaviourMode
    {
        Once,
        Repeat
    }

    private static readonly Dictionary<BehaviourMode, string> BehaviourModeLoc = new()
    {
        { BehaviourMode.Once, Service.Lang.GetText("AutoLogin-Once") },
        { BehaviourMode.Repeat, Service.Lang.GetText("AutoLogin-Repeat") },
    };
    private static bool HasLoginOnce;
    private static string WorldSearchInput = string.Empty;
    private static World? SelectedWorld;
    private static int SelectedCharaIndex;
    private static int _dropIndex = -1;

    private static Dictionary<uint, World>? Worlds;

    private static List<LoginInfo> LoginInfos = [];
    private static BehaviourMode Mode = BehaviourMode.Once;

    public override void Init()
    {
        AddConfig("LoginInfos", LoginInfos);
        LoginInfos = GetConfig<List<LoginInfo>>("LoginInfos");

        AddConfig("Mode", Mode);
        Mode = GetConfig<BehaviourMode>("Mode");

        Worlds ??= LuminaCache.Get<World>()
                          .Where(x => x.DataCenter.Value.Region == 5 && !string.IsNullOrWhiteSpace(x.Name.RawString) && !string.IsNullOrWhiteSpace(x.InternalName.RawString) && IsChineseString(x.Name.RawString))
                          .ToDictionary(x => x.RowId, x => x);

        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 20000, ShowDebug = false };
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "_TitleMenu", OnTitleMenu);
    }

    public override unsafe void ConfigUI()
    {
        ConflictKeyText();

        ImGui.AlignTextToFramePadding();
        ImGui.Text($"{Service.Lang.GetText("AutoLogin-LoginInfos")}:");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(200f * ImGuiHelpers.GlobalScale);
        if (ImGui.BeginCombo("###LoginInfosCombo", Service.Lang.GetText("AutoLogin-SavedLoginInfosAmount", LoginInfos.Count), ImGuiComboFlags.HeightLarge))
        {
            ImGui.BeginGroup();
            // 服务器选择
            ImGui.AlignTextToFramePadding();
            ImGui.Text($"{Service.Lang.GetText("AutoLogin-ServerName")}:");

            ImGui.SameLine();
            ImGui.SetNextItemWidth(120f * ImGuiHelpers.GlobalScale);
            CNWorldSelectCombo(ref SelectedWorld, ref WorldSearchInput);

            // 选择当前服务器
            ImGui.SameLine();
            if (ImGui.SmallButton(Service.Lang.GetText("AutoLogin-CurrentWorld")))
            {
                if (Worlds.TryGetValue(AgentLobby.Instance()->LobbyData.CurrentWorldId, out var world))
                    SelectedWorld = world;
            }

            // 角色登录索引选择
            ImGui.Text($"{Service.Lang.GetText("AutoLogin-CharacterIndex")}:");

            ImGui.SameLine();
            ImGui.SetNextItemWidth(120f * ImGuiHelpers.GlobalScale);
            if (ImGui.InputInt("##AutoLogin-EnterCharaIndex", ref SelectedCharaIndex, 0, 0, ImGuiInputTextFlags.EnterReturnsTrue))
                SelectedCharaIndex = Math.Clamp(SelectedCharaIndex, 0, 8);
            ImGuiOm.TooltipHover(Service.Lang.GetText("AutoLogin-CharaIndexInputTooltip"));
            ImGui.EndGroup();

            ImGui.SameLine();
            ImGui.Dummy(new(12));

            ImGui.SameLine();
            if (ImGuiOm.ButtonIconWithTextVertical(FontAwesomeIcon.Plus, Service.Lang.GetText("Add")))
            {
                if (SelectedCharaIndex is < 0 or > 7 || SelectedWorld == null) return;
                var info = new LoginInfo(SelectedWorld.RowId, SelectedCharaIndex);
                if (LoginInfos.Contains(info)) return;

                LoginInfos.Add(info);
                UpdateConfig("LoginInfos", LoginInfos);
            }
            ImGuiOm.TooltipHover(Service.Lang.GetText("AutoLogin-LoginInfoOrderHelp"));

            ImGui.Separator();
            ImGui.Separator();

            for (var i = 0; i < LoginInfos.Count; i++)
            {
                var info = LoginInfos[i];
                var world = LuminaCache.GetRow<World>(info.WorldID);

                ImGui.PushStyleColor(ImGuiCol.Text, i % 2 == 0 ? ImGuiColors.TankBlue : ImGuiColors.DalamudWhite);
                ImGui.Selectable($"{i + 1}. {Service.Lang.GetText("AutoLogin-LoginInfoDisplayText", world.Name.RawString, world.DataCenter.Value.Name.RawString, info.CharaIndex)}");
                ImGui.PopStyleColor();

                if (ImGui.BeginDragDropSource())
                {
                    if (ImGui.SetDragDropPayload("LoginInfoReorder", nint.Zero, 0)) _dropIndex = i;
                    ImGui.TextColored(ImGuiColors.DalamudYellow, Service.Lang.GetText("AutoLogin-LoginInfoDisplayText", world.Name.RawString, world.DataCenter.Value.Name.RawString, info.CharaIndex));
                    ImGui.EndDragDropSource();
                }

                if (ImGui.BeginDragDropTarget())
                {
                    if (ImGui.AcceptDragDropPayload("LoginInfoReorder").NativePtr != null)
                    {
                        Swap(_dropIndex, i);
                        _dropIndex = -1;
                    }

                    ImGui.EndDragDropTarget();
                }

                if (ImGui.BeginPopupContextItem($"ContextMenu_{i}"))
                {
                    if (ImGui.Selectable(Service.Lang.GetText("Delete")))
                        LoginInfos.Remove(info);
                    ImGui.EndPopup();
                }

                if (i != LoginInfos.Count - 1) ImGui.Separator();
            }

            ImGui.EndCombo();
        }

        ImGui.SameLine();
        ImGui.Text($"{Service.Lang.GetText("AutoLogin-BehaviourMode")}:");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(100f * ImGuiHelpers.GlobalScale);
        if (ImGui.BeginCombo("###BehaviourModeCombo", BehaviourModeLoc[Mode]))
        {
            foreach (var mode in BehaviourModeLoc)
            {
                if (ImGui.Selectable(mode.Value, mode.Key == Mode))
                {
                    Mode = mode.Key;
                    UpdateConfig("Mode", Mode);
                }
            }
            ImGui.EndCombo();
        }

        if (Mode == BehaviourMode.Once)
        {
            ImGui.SameLine();
            ImGui.Text($"{Service.Lang.GetText("AutoLogin-LoginState")}:");

            ImGui.SameLine();
            ImGui.TextColored(HasLoginOnce ? ImGuiColors.HealerGreen : ImGuiColors.DPSRed, HasLoginOnce ? Service.Lang.GetText("AutoLogin-LoginOnce") : Service.Lang.GetText("AutoLogin-HaveNotLogin"));

            ImGui.SameLine();
            if (ImGui.SmallButton(Service.Lang.GetText("Refresh")))
            {
                HasLoginOnce = false;
            }
        }
    }

    private void OnTitleMenu(AddonEvent eventType, AddonArgs? addonInfo)
    {
        if (LoginInfos.Count <= 0) return;
        if (Mode == BehaviourMode.Once && HasLoginOnce) return;

        if (InterruptByConflictKey()) return;

        TaskManager.Enqueue(SelectStartGame);
    }

    private unsafe bool? SelectStartGame()
    {
        if (InterruptByConflictKey()) return true;

        AgentManager.SendEvent(AgentId.Lobby, 0, 1);
        TaskManager.Enqueue(() => SelectCharacter());
        return true;
    }

    private unsafe bool? SelectCharacter(int infoIndex = 0)
    {
        if (InterruptByConflictKey()) return true;
        if (!EzThrottler.Throttle("AutoLogin", 100)) return false;

        if (Service.Gui.GetAddonByName("_TitleMenu") != nint.Zero) return false;
        var agent = AgentLobby.Instance();
        if (agent == null) return false;

        if (!TryGetAddonByName<AtkUnitBase>("_CharaSelectListMenu", out var addon) || !IsAddonAndNodesReady(addon)) return false;
        
        if (agent->WorldId == 0) return false;
        var requestedLoginInfo = LoginInfos[infoIndex];
        if (agent->WorldId == requestedLoginInfo.WorldID)
        {
            AddonManager.Callback(addon, true, 6, requestedLoginInfo.CharaIndex);
            AddonManager.Callback(addon, true, 18, 0, requestedLoginInfo.CharaIndex);
            AddonManager.Callback(addon, true, 6, requestedLoginInfo.CharaIndex);

            TaskManager.Enqueue(() => Click.TrySendClick("select_yes"));
            TaskManager.Enqueue(() => HasLoginOnce = true);
            return true;
        }

        TaskManager.Enqueue(SelectWorld);
        return true;
    }

    private unsafe bool? SelectWorld()
    {
        if (!EzThrottler.Throttle("AutoLogin", 100)) return false;
        if (InterruptByConflictKey()) return true;

        var agent = AgentLobby.Instance();
        if (agent == null) return false;

        if (!TryGetAddonByName<AtkUnitBase>("_CharaSelectWorldServer", out var addon)) return false;

        for (var infoIndex = 0; infoIndex < LoginInfos.Count; infoIndex++)
        {
            var loginInfo = LoginInfos[infoIndex];
            for (var i = 0; i < 16; i++)
            {
                AddonManager.Callback(addon, true, 9, 0, i);

                if (agent->WorldId == loginInfo.WorldID)
                {
                    AddonManager.Callback(addon, true, 10, 0, i);

                    TaskManager.DelayNext(200);
                    TaskManager.Enqueue(() => SelectCharacter(infoIndex));

                    return true;
                }
            }
        }

        TaskManager.Abort();
        return false;
    }

    private static bool IsChineseString(string text)
    {
        const int commonMin = 0x4e00;
        const int commonMax = 0x9fa5;
        const int extAMin = 0x3400;
        const int extAMax = 0x4db5;

        return text.All(c => (c >= commonMin && c <= commonMax) || (c >= extAMin && c <= extAMax));
    }

    private void Swap(int index1, int index2)
    {
        if (index1 < 0 || index1 > LoginInfos.Count || index2 < 0 || index2 > LoginInfos.Count) return;
        (LoginInfos[index1], LoginInfos[index2]) = (LoginInfos[index2], LoginInfos[index1]);

        TaskManager.Abort();

        TaskManager.DelayNext(500);
        TaskManager.Enqueue(() => { UpdateConfig("LoginInfos", LoginInfos); });
    }

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnTitleMenu);
        HasLoginOnce = false;

        base.Uninit();
    }
}
