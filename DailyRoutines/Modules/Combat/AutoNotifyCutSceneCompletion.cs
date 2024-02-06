using System;
using System.Diagnostics;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.AddonLifecycle;
using Dalamud.Game.ClientState.Conditions;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoNotifyCutSceneCompletionTitle", "AutoNotifyCutSceneCompletionDescription",
                   ModuleCategories.Combat)]
public class AutoNotifyCutSceneCompletion : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithConfigUI => false;

    private static TaskManager? TaskManager;

    private static bool IsDutyEnd;

    private static Stopwatch? Stopwatch;

    public void Init()
    {
        TaskManager ??= new TaskManager { ShowDebug = false, TimeLimitMS = int.MaxValue, AbortOnTimeout = false };
        Stopwatch ??= new Stopwatch();

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "_PartyList", OnPartyList);
        Service.DutyState.DutyCompleted += OnDutyComplete;
        Service.ClientState.TerritoryChanged += OnZoneChanged;
    }

    public void ConfigUI() { }

    public void OverlayUI() { }

    private static unsafe void OnPartyList(AddonEvent type, AddonArgs args)
    {
        if (TaskManager.IsBusy || Service.ClientState.IsPvP || !IsBoundByDuty() || IsDutyEnd) return;

        var isSBInCutScene = false;
        foreach (var member in Service.PartyList)
        {
            var chara = (Character*)member.GameObject.Address;
            if (chara == null) continue;
            if (!member.GameObject.IsTargetable)
            {
                isSBInCutScene = false;
                break;
            }

            if (chara->CharacterData.OnlineStatus == 15) isSBInCutScene = true;
        }

        if (isSBInCutScene)
        {
            Service.Log.Debug("检测到有成员正在过场动画中");
            Stopwatch.Restart();
            TaskManager.Enqueue(IsNoOneWatchingCutscene);
            TaskManager.Enqueue(
                () => Service.Notice.ShowWindowsToast(
                    "", Service.Lang.GetText("AutoNotifyCutSceneCompletion-NotificationMessage")));
        }
    }

    private static unsafe bool? IsNoOneWatchingCutscene()
    {
        if (IsDutyEnd)
        {
            Stopwatch.Reset();
            TaskManager.Abort();
            return true;
        }

        foreach (var member in Service.PartyList)
        {
            var chara = (Character*)member.GameObject.Address;
            if (chara == null) continue;
            if (chara->CharacterData.OnlineStatus == 15) return false;
        }

        if (Stopwatch.Elapsed < TimeSpan.FromSeconds(5))
        {
            Stopwatch.Reset();
            TaskManager.Abort();
        }

        return true;
    }

    private void OnZoneChanged(object? sender, ushort e)
    {
        Stopwatch.Reset();
        TaskManager.Abort();
        IsDutyEnd = false;
    }

    private void OnDutyComplete(object? sender, ushort e)
    {
        Stopwatch.Reset();
        TaskManager.Abort();
        IsDutyEnd = true;
    }

    public static bool IsBoundByDuty()
    {
        return Service.Condition[ConditionFlag.BoundByDuty] || Service.Condition[ConditionFlag.BoundByDuty56] ||
               Service.Condition[ConditionFlag.BoundByDuty95];
    }

    public void Uninit()
    {
        TaskManager?.Abort();
        Stopwatch.Reset();

        Service.DutyState.DutyCompleted -= OnDutyComplete;
        Service.ClientState.TerritoryChanged -= OnZoneChanged;
        Service.AddonLifecycle.UnregisterListener(OnPartyList);
    }
}
