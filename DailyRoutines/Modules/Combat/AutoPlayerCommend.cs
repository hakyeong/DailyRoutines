using System;
using System.Linq;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Memory;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoPlayerCommendTitle", "AutoPlayerCommendDescription", ModuleCategories.Combat)]
public class AutoPlayerCommend : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithConfigUI => false;

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

    private static TaskManager? TaskManager;

    public void Init()
    {
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 10000, ShowDebug = false };
        Service.DutyState.DutyCompleted += OnDutyComplete;
    }

    public void ConfigUI() { }

    public void OverlayUI() { }

    private void OnDutyComplete(object? sender, ushort dutyID)
    {
        TaskManager.Enqueue(OpenCommendWindow);
        TaskManager.DelayNext(250);
        TaskManager.Enqueue(CommendPlayer);
    }

    private unsafe bool? OpenCommendWindow()
    {
        if (IsOccupied()) return false;

        if (Service.Gui.GetAddonByName("VoteMvp") != nint.Zero) return true;

        var notification = (AtkUnitBase*)Service.Gui.GetAddonByName("_Notification");
        if (notification == null) return false;

        AddonManager.Callback(notification, true, 0, 11);
        AddonManager.Callback(notification, true, 0, 11, "");
        return true;
    }

    private unsafe bool? CommendPlayer()
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

        if (allies.Count <= 0) return true;
        var playersToCommend = allies.Values
                                    .OrderByDescending(x => x.Role == localPlayerRole)
                                    .ThenByDescending(x =>
                                    {
                                        switch (localPlayerRole)
                                        {
                                            case PlayerRole.Tank or PlayerRole.Healer:
                                                return x.Role is PlayerRole.Tank or PlayerRole.Healer ? 1 : 0;
                                            case PlayerRole.DPS or PlayerRole.DPS:
                                                switch (x.Role)
                                                {
                                                    case PlayerRole.DPS or PlayerRole.DPS:
                                                        return 3;
                                                    case PlayerRole.Healer:
                                                        return 2;
                                                    case PlayerRole.Tank:
                                                        return 1;
                                                }

                                                break;
                                        }
                                        return 0;
                                    });

        if (TryGetAddonByName<AtkUnitBase>("VoteMvp", out var addon) && HelpersOm.IsAddonAndNodesReady(addon))
        {
            var commendSuccess = false;
            var commendIndex = -1;
            var commendPlayerName = string.Empty;

            foreach (var playerToCommend in playersToCommend)
            {
                for (var i = 0; i < allies.Count; ++i)
                {
                    if (!Convert.ToBoolean(addon->AtkValues[i + 16].Byte)) continue;

                    try
                    {
                        var playerNameInAddon = MemoryHelper.ReadStringNullTerminated((nint)addon->AtkValues[i + 9].String);
                        if (playerNameInAddon == playerToCommend.PlayerName)
                        {
                            commendIndex = i;
                            commendPlayerName = playerToCommend.PlayerName;
                            break;
                        }
                    }
                    catch
                    {
                        return false;
                    }
                }

                if (commendIndex != -1)
                {
                    AddonManager.Callback(addon, true, 0, commendIndex);
                    Service.Chat.Print(Service.Lang.GetSeString("AutoPlayerCommend-NoticeMessage", commendPlayerName));
                    commendSuccess = true;
                    break;
                }
            }

            return commendSuccess;
        }

        if (TryGetAddonByName<AtkUnitBase>("BannerMIP", out var addonBanner) && HelpersOm.IsAddonAndNodesReady(addonBanner))
        {
            var commendSuccess = false;
            var commendIndex = -1;
            var commendPlayerName = string.Empty;

            foreach (var playerToCommend in playersToCommend)
            {
                for (var i = 0; i < allies.Count; ++i)
                {
                    if (!Convert.ToBoolean(addonBanner->AtkValues[i + 29].Byte)) continue;

                    try
                    {
                        var playerNameInAddon = MemoryHelper.ReadStringNullTerminated((nint)addonBanner->AtkValues[i + 22].String);
                        if (playerNameInAddon == playerToCommend.PlayerName)
                        {
                            commendIndex = i;
                            commendPlayerName = playerToCommend.PlayerName;
                            break;
                        }
                    }
                    catch
                    {
                        return false;
                    }
                }

                if (commendIndex != -1)
                {
                    AddonManager.Callback(addonBanner, true, 12, commendIndex);
                    Service.Chat.Print(Service.Lang.GetSeString("AutoPlayerCommend-NoticeMessage", commendPlayerName));
                    commendSuccess = true;
                    break;
                }
            }

            return commendSuccess;
        }

        return false;
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

    public void Uninit()
    {
        TaskManager?.Abort();
        Service.DutyState.DutyCompleted -= OnDutyComplete;
    }
}
