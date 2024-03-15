using System;
using System.Linq;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Memory;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoPlayerCommendTitle", "AutoPlayerCommendDescription", ModuleCategories.Combat)]
public unsafe class AutoPlayerCommend : DailyModuleBase
{
    private enum PlayerRole
    {
        Tank,
        Healer,
        DPS,
        None
    }

    private class PlayerInfo(string name, PlayerRole role, string job)
    {
        public string PlayerName { get; set; } = name;
        public PlayerRole Role { get; set; } = role;
        public string Job { get; set; } = job;
    }

    public override void Init()
    {
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 10000, ShowDebug = false };
        Service.DutyState.DutyCompleted += OnDutyComplete;

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, "VoteMvp", OnAddonList);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, "BannerMIP", OnAddonList);
    }

    private void OnDutyComplete(object? sender, ushort dutyID)
    {
        TaskManager.Enqueue(OpenCommendWindow);
    }

    private static bool? OpenCommendWindow()
    {
        if (IsOccupied()) return false;

        if (Service.Gui.GetAddonByName("VoteMvp") != nint.Zero) return true;

        var notification = (AtkUnitBase*)Service.Gui.GetAddonByName("_Notification");
        if (notification == null) return false;

        AddonManager.Callback(notification, true, 0, 11);
        AddonManager.Callback(notification, true, 0, 11, "");
        return true;
    }

    private static void ProcessCommendation(string addonName, int voteOffset, int nameOffset, int callbackIndex)
    {
        var localPlayer = Service.ClientState.LocalPlayer;
        var localPlayerName = localPlayer.Name.ExtractText();
        var localPlayerRole = GetCharacterJobRole(localPlayer.ClassJob.GameData.Role);
        var allies = Service.PartyList
                            .Where(ally => ally.Name.ExtractText() != localPlayerName)
                            .ToDictionary(ally => ally.Address,
                                          ally => new PlayerInfo(ally.Name.ExtractText(),
                                                                 GetCharacterJobRole(ally.ClassJob.GameData.Role),
                                                                 ally.ClassJob.GameData.Name.ExtractText()));

        if (allies.Count == 0) return;

        var playersToCommend = allies.Values
                                     .OrderByDescending(player => player.Role == localPlayerRole)
                                     .ThenByDescending(player =>
                                     {
                                         return localPlayerRole switch
                                         {
                                             PlayerRole.Tank or PlayerRole.Healer => player.Role is PlayerRole.Tank
                                                 or PlayerRole.Healer
                                                 ? 1
                                                 : 0,
                                             PlayerRole.DPS => player.Role switch
                                             {
                                                 PlayerRole.DPS => 3,
                                                 PlayerRole.Healer => 2,
                                                 PlayerRole.Tank => 1,
                                                 _ => 0
                                             },
                                             _ => 0
                                         };
                                     });

        if (TryGetAddonByName<AtkUnitBase>(addonName, out var addon) && HelpersOm.IsAddonAndNodesReady(addon))
        {
            foreach (var player in playersToCommend)
                for (var i = 0; i < allies.Count; i++)
                    if (Convert.ToBoolean(addon->AtkValues[i + voteOffset].Byte))
                    {
                        var playerNameInAddon =
                            MemoryHelper.ReadStringNullTerminated((nint)addon->AtkValues[i + nameOffset].String);
                        if (playerNameInAddon == player.PlayerName)
                        {
                            AddonManager.Callback(addon, true, callbackIndex, i);
                            Service.Chat.Print(
                                Service.Lang.GetSeString("AutoPlayerCommend-NoticeMessage", player.Job,
                                                         player.PlayerName));
                            return;
                        }
                    }
        }
    }

    private static void OnAddonList(AddonEvent type, AddonArgs args)
    {
        switch (args.AddonName)
        {
            case "VoteMvp":
                ProcessCommendation("VoteMvp", 16, 9, 0);
                break;
            case "BannerMIP":
                ProcessCommendation("BannerMIP", 29, 22, 12);
                break;
        }
    }

    private static PlayerRole GetCharacterJobRole(byte rawRole)
    {
        return rawRole switch
        {
            1 => PlayerRole.Tank,
            2 or 3 => PlayerRole.DPS,
            4 => PlayerRole.Healer,
            _ => PlayerRole.None
        };
    }

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddonList);
        Service.DutyState.DutyCompleted -= OnDutyComplete;

        base.Uninit();
    }
}
