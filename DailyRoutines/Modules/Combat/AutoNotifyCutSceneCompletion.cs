using System.Threading.Tasks;
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

    public void Init()
    {
        TaskManager ??= new TaskManager { ShowDebug = false, TimeLimitMS = int.MaxValue, AbortOnTimeout = false };

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "_PartyList", OnPartyList);
        Service.DutyState.DutyCompleted += OnDutyComplete;
        Service.ClientState.TerritoryChanged += OnZoneChanged;
    }

    public void ConfigUI() { }

    public void OverlayUI() { }

    private static void OnPartyList(AddonEvent type, AddonArgs args)
    {
        if (TaskManager.IsBusy || !IsBoundByDuty() || IsDutyEnd) return;

        Task.Delay(5000).ContinueWith(_ => DelayMemberStatsCheck());
    }

    private static unsafe void DelayMemberStatsCheck()
    {
        if (TaskManager.IsBusy || !IsBoundByDuty() || IsDutyEnd) return;

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
            TaskManager.Enqueue(IsNoOneWatchingCutscene);
            TaskManager.Enqueue(
                () => NotificationManager.ShowWindowsToast(
                    "", Service.Lang.GetText("AutoNotifyCutSceneCompletion-NotificationMessage")));
        }
    }

    private static unsafe bool? IsNoOneWatchingCutscene()
    {
        if (IsDutyEnd)
        {
            TaskManager.Abort();
            return true;
        }

        foreach (var member in Service.PartyList)
        {
            var chara = (Character*)member.GameObject.Address;
            if (chara == null) continue;
            if (chara->CharacterData.OnlineStatus == 15) return false;
        }

        return true;
    }

    private void OnZoneChanged(object? sender, ushort e)
    {
        IsDutyEnd = false;
    }

    private void OnDutyComplete(object? sender, ushort e)
    {
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

        Service.DutyState.DutyCompleted -= OnDutyComplete;
        Service.ClientState.TerritoryChanged -= OnZoneChanged;
        Service.AddonLifecycle.UnregisterListener(OnPartyList);
    }
}
