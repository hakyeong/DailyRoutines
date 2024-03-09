using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text.SeStringHandling;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoMountTitle", "AutoMountDescription", ModuleCategories.General)]
public class AutoMount : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithConfigUI => false;

    private static TaskManager? TaskManager;

    public void Init()
    {
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 20000, ShowDebug = false };

        Service.Condition.ConditionChange += OnConditionChanged;
        Service.ClientState.TerritoryChanged += OnZoneChanged;
        Service.Toast.ErrorToast += OnErrorToast;
    }

    public void ConfigUI() { }

    public void OverlayUI() { }
    
    private static void OnZoneChanged(ushort obj)
    {
        TaskManager.Enqueue(UseMountWhenZoneChanged);
    }

    private static void OnConditionChanged(ConditionFlag flag, bool value)
    {
        if (flag is ConditionFlag.Gathering && !value)
        {
            TaskManager.Enqueue(UseMountAfterGathering);
        }
    }

    private static void OnErrorToast(ref SeString message, ref bool isHandled)
    {
        if (!TaskManager.IsBusy) return;

        if (message.ExtractText().Contains("这里无法呼叫出坐骑"))
        {
            message = SeString.Empty;
            TaskManager.Abort();
        }
    }

    private static unsafe bool? UseMountAfterGathering()
    {
        if (AgentMap.Instance()->IsPlayerMoving == 1) return true;
        if (Service.Condition[ConditionFlag.Mounted] || Service.Condition[ConditionFlag.Mounted2] || Service.Condition[ConditionFlag.Casting] || Service.Condition[ConditionFlag.Casting87]) return true;
        if (ActionManager.Instance()->GetActionStatus(ActionType.GeneralAction, 9) != 0) return false;

        TaskManager.DelayNext(100);
        TaskManager.Enqueue(() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 9));

        return true;
    }

    private static unsafe bool? UseMountWhenZoneChanged()
    {
        if (Service.Condition[ConditionFlag.BetweenAreas]) return false;
        if (Service.Condition[ConditionFlag.Mounted] || Service.Condition[ConditionFlag.Mounted2]) return true;

        TaskManager.DelayNext(100);
        TaskManager.Enqueue(() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 9));

        return true;
    }

    public void Uninit()
    {
        TaskManager?.Abort();
        Service.ClientState.TerritoryChanged -= OnZoneChanged;
        Service.Condition.ConditionChange -= OnConditionChanged;
        Service.Toast.ErrorToast -= OnErrorToast;
    }
}
