using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using ClickLib;
using ClickLib.Clicks;
using DailyRoutines.Helpers;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoLoginTitle", "AutoLoginDescription", ModuleCategories.一般)]
public unsafe class AutoLogin : DailyModuleBase
{
    private static readonly Dictionary<BehaviourMode, string> BehaviourModeLoc = new()
    {
        { BehaviourMode.Once, Service.Lang.GetText("AutoLogin-Once") },
        { BehaviourMode.Repeat, Service.Lang.GetText("AutoLogin-Repeat") },
    };

    private const string Command = "/pdrlogin";

    private static Config ModuleConfig = null!;
    private static readonly Throttler<string> Throttler = new();

    private static Dictionary<uint, World>? Worlds;
    private static World? SelectedWorld;
    private static string WorldSearchInput = string.Empty;
    private static int SelectedCharaIndex;
    private static int _dropIndex = -1;

    private static bool HasLoginOnce;
    private static ushort ManualWorldID;
    private static int ManualCharaIndex = -1;

    public override void Init()
    {
        ModuleConfig = LoadConfig<Config>() ?? new();

        Worlds ??= LuminaCache.Get<World>()
                              .Where(x => x.DataCenter.Value.Region == 5 &&
                                          !string.IsNullOrWhiteSpace(x.Name.RawString) &&
                                          !string.IsNullOrWhiteSpace(x.InternalName.RawString) &&
                                          IsChineseString(x.Name.RawString))
                              .ToDictionary(x => x.RowId, x => x);

        TaskHelper ??= new TaskHelper { AbortOnTimeout = true, TimeLimitMS = 10000, ShowDebug = false };
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "_TitleMenu", OnTitleMenu);

        if (ModuleConfig.AddCommand)
            Service.CommandManager.AddCommand(Command, new(OnCommand)
            {
                HelpMessage = Service.Lang.GetText("AutoLogin-CommandHelp"),
            });
    }

    public override void ConfigUI()
    {
        ConflictKeyText();

        if (ImGui.Checkbox(Service.Lang.GetText("AutoLogin-AddCommand", Command), ref ModuleConfig.AddCommand))
        {
            if (ModuleConfig.AddCommand)
                Service.CommandManager.AddCommand(Command, new(OnCommand)
                {
                    HelpMessage = Service.Lang.GetText("AutoLogin-CommandHelp"),
                });
            else
                Service.CommandManager.RemoveCommand(Command);
        }

        ImGuiOm.HelpMarker(Service.Lang.GetText("AutoLogin-AddCommandHelp", Command, Command));

        ImGui.AlignTextToFramePadding();
        ImGui.Text($"{Service.Lang.GetText("AutoLogin-LoginInfos")}:");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(200f * ImGuiHelpers.GlobalScale);
        if (ImGui.BeginCombo("###LoginInfosCombo",
                             Service.Lang.GetText("AutoLogin-SavedLoginInfosAmount", ModuleConfig.LoginInfos.Count),
                             ImGuiComboFlags.HeightLarge))
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
            if (ImGui.InputInt("##AutoLogin-EnterCharaIndex", ref SelectedCharaIndex, 0, 0,
                               ImGuiInputTextFlags.EnterReturnsTrue))
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
                if (!ModuleConfig.LoginInfos.Contains(info))
                {
                    ModuleConfig.LoginInfos.Add(info);
                    SaveConfig(ModuleConfig);
                }
            }

            ImGuiOm.TooltipHover(Service.Lang.GetText("AutoLogin-LoginInfoOrderHelp"));

            ImGui.Separator();
            ImGui.Separator();

            for (var i = 0; i < ModuleConfig.LoginInfos.Count; i++)
            {
                var info = ModuleConfig.LoginInfos[i];
                var world = LuminaCache.GetRow<World>(info.WorldID);

                ImGui.PushStyleColor(ImGuiCol.Text, i % 2 == 0 ? ImGuiColors.TankBlue : ImGuiColors.DalamudWhite);
                ImGui.Selectable(
                    $"{i + 1}. {Service.Lang.GetText("AutoLogin-LoginInfoDisplayText", world.Name.RawString, world.DataCenter.Value.Name.RawString, info.CharaIndex)}");

                ImGui.PopStyleColor();

                if (ImGui.BeginDragDropSource())
                {
                    if (ImGui.SetDragDropPayload("LoginInfoReorder", nint.Zero, 0)) _dropIndex = i;
                    ImGui.TextColored(ImGuiColors.DalamudYellow,
                                      Service.Lang.GetText("AutoLogin-LoginInfoDisplayText", world.Name.RawString,
                                                           world.DataCenter.Value.Name.RawString, info.CharaIndex));

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
                    {
                        ModuleConfig.LoginInfos.Remove(info);
                        SaveConfig(ModuleConfig);
                    }

                    ImGui.EndPopup();
                }

                if (i != ModuleConfig.LoginInfos.Count - 1) ImGui.Separator();
            }

            ImGui.EndCombo();
        }

        ImGui.SameLine();
        ImGui.Text($"{Service.Lang.GetText("AutoLogin-BehaviourMode")}:");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(100f * ImGuiHelpers.GlobalScale);
        if (ImGui.BeginCombo("###BehaviourModeCombo", BehaviourModeLoc[ModuleConfig.Mode]))
        {
            foreach (var mode in BehaviourModeLoc)
                if (ImGui.Selectable(mode.Value, mode.Key == ModuleConfig.Mode))
                {
                    ModuleConfig.Mode = mode.Key;
                    SaveConfig(ModuleConfig);
                }

            ImGui.EndCombo();
        }

        if (ModuleConfig.Mode == BehaviourMode.Once)
        {
            ImGui.SameLine();
            ImGui.Text($"{Service.Lang.GetText("AutoLogin-LoginState")}:");

            ImGui.SameLine();
            ImGui.TextColored(HasLoginOnce ? ImGuiColors.HealerGreen : ImGuiColors.DPSRed,
                              HasLoginOnce
                                  ? Service.Lang.GetText("AutoLogin-LoginOnce")
                                  : Service.Lang.GetText("AutoLogin-HaveNotLogin"));

            ImGui.SameLine();
            if (ImGui.SmallButton(Service.Lang.GetText("Refresh"))) HasLoginOnce = false;
        }
    }

    private void OnCommand(string command, string args)
    {
        args = args.Trim();
        if (string.IsNullOrWhiteSpace(args)) return;
        if (!Service.ClientState.IsLoggedIn || Service.ClientState.LocalPlayer == null || 
            Flags.BoundByDuty) return;

        var parts = args.Split(' ');
        switch (parts.Length)
        {
            case 1:
                if (!int.TryParse(args, out var charaIndex0) || charaIndex0 < 0 || charaIndex0 > 8) return;

                ManualWorldID = (ushort)Service.ClientState.LocalPlayer.HomeWorld.Id;
                ManualCharaIndex = charaIndex0;
                break;
            case 2:
                var world1 = Worlds.FirstOrDefault(x => x.Value.Name.RawString.Contains(parts[0])).Key;
                if (world1 == 0) return;
                if (!int.TryParse(parts[1], out var charaIndex1) || charaIndex1 < 0 || charaIndex1 > 8) return;

                ManualWorldID = (ushort)world1;
                ManualCharaIndex = charaIndex1;
                break;
            default:
                return;
        }

        TaskHelper.Abort();
        TaskHelper.Enqueue(Logout);
    }

    private static bool? Logout()
    {
        if (!Throttler.Throttle("Logout")) return false;
        if (!Service.ClientState.IsLoggedIn) return true;

        if (AddonState.SelectYesno == null)
        {
            ChatHelper.Instance.SendMessage("/logout");
            return false;
        }

        var click = new ClickSelectYesNo();
        var title = Marshal.PtrToStringUTF8((nint)AddonState.SelectYesno->AtkValues[0].String);
        if (!title.Contains(LuminaCache.GetRow<Addon>(115).Text.RawString))
        {
            click.No();
            return false;
        }

        click.Yes();
        return true;
    }

    private void OnTitleMenu(AddonEvent eventType, AddonArgs? addonInfo)
    {
        if (ModuleConfig.LoginInfos.Count <= 0) return;
        if (ModuleConfig.Mode == BehaviourMode.Once && HasLoginOnce) return;
        if (InterruptByConflictKey()) return;

        AgentHelper.SendEvent(AgentId.Lobby, 0, 1);

        TaskHelper.Abort();
        if (ManualWorldID != 0 && ManualCharaIndex != -1)
            TaskHelper.Enqueue(() => SelectCharacter(ManualWorldID, ManualCharaIndex), "SelectCharaManual");
        else
            TaskHelper.Enqueue(SelectCharacterDefault, "SelectCharaDefault0");
    }

    private void SelectCharacterDefault()
    {
        var info = ModuleConfig.LoginInfos.FirstOrDefault();
        if (info == null)
        {
            TaskHelper.Abort();
            return;
        }

        TaskHelper.Enqueue(() => SelectCharacter((ushort)info.WorldID, info.CharaIndex), "SelectCharaDefault1");
    }

    private bool? SelectCharacter(ushort worldID, int charaIndex)
    {
        if (InterruptByConflictKey()) return true;
        if (!Throttler.Throttle("SelectCharacter", 100)) return false;

        var agent = AgentLobby.Instance();
        if (agent == null) return false;

        var addon = AddonState.CharaSelectListMenu;
        if (addon == null || !IsAddonAndNodesReady(addon)) return false;

        if (agent->WorldId == 0) return false;
        if (agent->WorldId != worldID)
        {
            TaskHelper.Enqueue(() => SelectWorld(worldID), "SelectWorld", 2);
            TaskHelper.Enqueue(() => SelectCharacter(worldID, charaIndex));
            return true;
        }

        Callback(addon, true, 6, charaIndex);
        Callback(addon, true, 18, 0, charaIndex);
        Callback(addon, true, 6, charaIndex);

        TaskHelper.Enqueue(() => Click.TrySendClick("select_yes"));
        TaskHelper.Enqueue(ResetStates);
        return true;
    }

    private bool? SelectWorld(ushort worldID)
    {
        if (InterruptByConflictKey()) return true;
        if (!Throttler.Throttle("SelectWorld", 100)) return false;

        var agent = AgentLobby.Instance();
        if (agent == null) return false;

        var addon = AddonState.CharaSelectWorldServer;
        if (addon == null || !IsAddonAndNodesReady(addon)) return false;

        for (var i = 0; i < 16; i++)
        {
            Callback(addon, true, 9, 0, i);

            if (agent->WorldId == worldID)
            {
                Callback(addon, true, 10, 0, i);
                return true;
            }
        }

        TaskHelper.Abort();
        NotifyHelper.NotificationError("没有找到对应的服务器");
        return true;
    }

    private static void ResetStates()
    {
        HasLoginOnce = true;
        ManualWorldID = 0;
        ManualCharaIndex = -1;
    }

    private void Swap(int index1, int index2)
    {
        if (index1 < 0 || index1 > ModuleConfig.LoginInfos.Count || 
            index2 < 0 || index2 > ModuleConfig.LoginInfos.Count) return;
        (ModuleConfig.LoginInfos[index1], ModuleConfig.LoginInfos[index2]) = (ModuleConfig.LoginInfos[index2], ModuleConfig.LoginInfos[index1]);

        TaskHelper.Abort();

        TaskHelper.DelayNext(500);
        TaskHelper.Enqueue(() => SaveConfig(ModuleConfig));
    }

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnTitleMenu);
        Service.CommandManager.RemoveCommand(Command);
        ResetStates();
        HasLoginOnce = false;

        base.Uninit();
    }

    private class Config : ModuleConfiguration
    {
        public bool AddCommand = true;
        public List<LoginInfo> LoginInfos = [];
        public BehaviourMode Mode = BehaviourMode.Once;
    }

    private class LoginInfo(uint worldID, int index) : IEquatable<LoginInfo>
    {
        public uint WorldID    { get; set; } = worldID;
        public int  CharaIndex { get; set; } = index;

        public bool Equals(LoginInfo? other)
        {
            if (other is null || GetType() != other.GetType())
                return false;

            return WorldID == other.WorldID && CharaIndex == other.CharaIndex;
        }

        public override bool Equals(object? obj) { return Equals(obj as LoginInfo); }

        public override int GetHashCode() { return HashCode.Combine(WorldID, CharaIndex); }

        public static bool operator ==(LoginInfo? lhs, LoginInfo? rhs)
        {
            if (lhs is null) return rhs is null;
            return lhs.Equals(rhs);
        }

        public static bool operator !=(LoginInfo lhs, LoginInfo rhs) { return !(lhs == rhs); }
    }

    private enum BehaviourMode
    {
        Once,
        Repeat,
    }
}
