using System;
using System.Diagnostics;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using DailyRoutines.Notifications;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoNotifyCutSceneEndTitle", "AutoNotifyCutSceneEndDescription", ModuleCategories.Notice)]
public class AutoNotifyCutSceneEnd : DailyModuleBase
{
    private static bool ConfigOnlyNotifyWhenBackground;
    private static bool IsDutyEnd;

    private static Stopwatch? Stopwatch;

    public override void Init()
    {
        TaskManager ??= new TaskManager { ShowDebug = false, TimeLimitMS = int.MaxValue, AbortOnTimeout = false };
        Stopwatch ??= new Stopwatch();

        AddConfig("OnlyNotifyWhenBackground", true);
        ConfigOnlyNotifyWhenBackground = GetConfig<bool>("OnlyNotifyWhenBackground");

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "_PartyList", OnPartyList);
        Service.DutyState.DutyCompleted += OnDutyComplete;
        Service.ClientState.TerritoryChanged += OnZoneChanged;
    }

    public override void ConfigUI()
    {
        PreviewImageWithHelpText(Service.Lang.GetText("AutoNotifyCutSceneEnd-NotificationMessageHelp"),
                                 "https://gh.atmoomen.top/DailyRoutines/main/Assets/Images/AutoNotifyCutSceneEnd-1.png");

        if (ImGui.Checkbox(Service.Lang.GetText("OnlyNotifyWhenBackground"),
                           ref ConfigOnlyNotifyWhenBackground))
            UpdateConfig("OnlyNotifyWhenBackground", ConfigOnlyNotifyWhenBackground);
    }

    private unsafe void OnPartyList(AddonEvent type, AddonArgs args)
    {
        if (TaskManager.IsBusy || Service.ClientState.IsPvP || !Flags.BoundByDuty() || IsDutyEnd) return;

        var isSBInCutScene = false;
        foreach (var member in Service.PartyList)
        {
            if (member.GameObject == null) continue;

            var chara = (Character*)member.GameObject.Address;
            if (chara == null) continue;
            if (!Service.DutyState.IsDutyStarted && !member.GameObject.IsTargetable)
            {
                isSBInCutScene = false;
                break;
            }

            if (chara->CharacterData.OnlineStatus == 15) isSBInCutScene = true;
        }

        if (isSBInCutScene)
        {
            Stopwatch.Restart();
            TaskManager.Enqueue(WaitAllMembersCutsceneEnd);
            TaskManager.Enqueue(ShowNoticeMessage);
        }
    }

    private unsafe bool? WaitAllMembersCutsceneEnd()
    {
        if (IsDutyEnd)
        {
            Stopwatch.Reset();
            TaskManager.Abort();
            return true;
        }

        foreach (var member in Service.PartyList)
        {
            if (member.GameObject == null) continue;

            var chara = (Character*)member.GameObject.Address;
            if (chara == null) continue;
            if (chara->CharacterData.OnlineStatus == 15) return false;
        }

        if (Stopwatch.Elapsed < TimeSpan.FromSeconds(4))
        {
            Stopwatch.Reset();
            TaskManager.Abort();
        }

        return true;
    }

    private static bool? ShowNoticeMessage()
    {
        if (ConfigOnlyNotifyWhenBackground)
        {
            if (!HelpersOm.IsGameForeground())
                WinToast.Notify("", Service.Lang.GetText("AutoNotifyCutSceneEnd-NotificationMessage"));
            return true;
        }

        WinToast.Notify("", Service.Lang.GetText("AutoNotifyCutSceneEnd-NotificationMessage"));
        return true;
    }

    private void OnZoneChanged(ushort zone)
    {
        Stopwatch.Reset();
        TaskManager.Abort();
        IsDutyEnd = false;
    }

    private void OnDutyComplete(object? sender, ushort duty)
    {
        Stopwatch.Reset();
        TaskManager.Abort();
        IsDutyEnd = true;
    }

    public override void Uninit()
    {
        Stopwatch.Reset();

        Service.DutyState.DutyCompleted -= OnDutyComplete;
        Service.ClientState.TerritoryChanged -= OnZoneChanged;
        Service.AddonLifecycle.UnregisterListener(OnPartyList);

        base.Uninit();
    }
}
