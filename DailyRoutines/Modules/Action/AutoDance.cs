using DailyRoutines.Managers;
using Dalamud.Game.ClientState.JobGauge.Types;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using TaskManager = ECommons.Automation.LegacyTaskManager.TaskManager;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoDanceTitle", "AutoDanceDescription", ModuleCategories.技能)]
public unsafe class AutoDance : DailyModuleBase
{
    public override void Init()
    {
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = false };

        Service.UseActionManager.Register(OnPostUseAction);
    }

    private void OnPostUseAction(
        bool result, ActionType actionType, uint actionID, ulong targetID, uint a4, uint queueState, uint a6)
    {
        var gauge = Service.JobGauges.Get<DNCGauge>();
        if (result && actionType is ActionType.Action && actionID is 15997 or 15998 && !gauge.IsDancing)
        {
            TaskManager.Enqueue(() => gauge.IsDancing);
            TaskManager.Enqueue(actionID == 15997 ? DanceStandardStep : DanceTechnicalStep);
        }
    }

    private bool? DanceStandardStep() => DanceStep(false);

    private bool? DanceTechnicalStep() => DanceStep(true);

    private bool? DanceStep(bool isTechnicalStep)
    {
        if (!EzThrottler.Throttle("AutoDance", 200)) return false;
        var gauge = Service.JobGauges.Get<DNCGauge>();
        if (!gauge.IsDancing)
        {
            TaskManager.Abort();
            return true;
        }

        var nextStep = gauge.NextStep;
        if (gauge.CompletedSteps < (isTechnicalStep ? 4 : 2))
        {
            if (ActionManager.Instance()->UseAction(ActionType.Action, nextStep))
            {
                TaskManager.Enqueue(() => DanceStep(isTechnicalStep));
                return true;
            }
        }

        return false;
    }

    public override void Uninit()
    {
        Service.UseActionManager.Unregister(OnPostUseAction);

        base.Uninit();
    }
}
