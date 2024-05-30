using DailyRoutines.Infos;
using DailyRoutines.Managers;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoDrawACardTitle", "AutoDrawACardDescription", ModuleCategories.技能)]
public class AutoDrawACard : DailyModuleBase
{
    public override void Init()
    {
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 30000, ShowDebug = false };

        Service.ClientState.TerritoryChanged += OnZoneChanged;
        Service.DutyState.DutyRecommenced += OnDutyRecommenced;
    }

    private void OnDutyRecommenced(object? sender, ushort e)
    {
        TaskManager.Abort();
        TaskManager.Enqueue(CheckCurrentJob);
    }

    private void OnZoneChanged(ushort zone)
    {
        if (!PresetData.Contents.ContainsKey(zone) || Service.ClientState.IsPvP) return;

        TaskManager.Abort();
        TaskManager.DelayNext(100);
        TaskManager.Enqueue(CheckCurrentJob);
    }

    private static unsafe bool? CheckCurrentJob()
    {
        if (Flags.BetweenAreas) return false;

        var player = Service.ClientState.LocalPlayer;
        if (player == null || player.ClassJob.Id == 0 || !player.IsTargetable) return false;

        if (player.ClassJob.Id != 33 || player.Level < 30) return true;
        if (IsOccupied()) return false;

        return ActionManager.Instance()->UseAction(ActionType.Action, 3590);
    }

    public override void Uninit()
    {
        Service.ClientState.TerritoryChanged -= OnZoneChanged;
        Service.DutyState.DutyRecommenced -= OnDutyRecommenced;

        base.Uninit();
    }
}
