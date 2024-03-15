using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text.SeStringHandling;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoMountTitle", "AutoMountDescription", ModuleCategories.General)]
public class AutoMount : DailyModuleBase
{
    public override void Init()
    {
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 20000, ShowDebug = false };

        Service.Condition.ConditionChange += OnConditionChanged;
        Service.ClientState.TerritoryChanged += OnZoneChanged;
        Service.Toast.ErrorToast += OnErrorToast;
    }
    
    private void OnZoneChanged(ushort zone)
    {
        if (Service.PresetData.Contents.ContainsKey(zone)) return;

        TaskManager.Abort();
        TaskManager.Enqueue(UseMountWhenZoneChanged);
    }

    private void OnConditionChanged(ConditionFlag flag, bool value)
    {
        if (flag is ConditionFlag.Gathering && !value)
        {
            TaskManager.Abort();
            TaskManager.Enqueue(UseMountAfterGathering);
        }
    }

    private void OnErrorToast(ref SeString message, ref bool isHandled)
    {
        if (!TaskManager.IsBusy) return;
        var content = message.ExtractText();

        if (content.Contains("无法指定目标"))
        {
            message = SeString.Empty;
            return;
        }

        if (content.Contains("这里无法呼叫出坐骑"))
        {
            message = SeString.Empty;
            TaskManager.Abort();
        }
    }

    private unsafe bool? UseMountAfterGathering()
    {
        if (AgentMap.Instance()->IsPlayerMoving == 1) return true;
        if (Service.Condition[ConditionFlag.Casting] | Service.Condition[ConditionFlag.Casting87]) return true;
        if (Service.Condition[ConditionFlag.Mounted] || Service.Condition[ConditionFlag.Mounted2]) return true;
        if (ActionManager.Instance()->GetActionStatus(ActionType.GeneralAction, 9) != 0) return false;

        TaskManager.DelayNext(100);
        TaskManager.Enqueue(() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 9));

        return true;
    }

    private unsafe bool? UseMountWhenZoneChanged()
    {
        if (Service.Condition[ConditionFlag.BetweenAreas]) return false;
        var addon = (AtkUnitBase*)Service.Gui.GetAddonByName("NowLoading");
        if (addon->IsVisible) return false;
        var addon2 = (AtkUnitBase*)Service.Gui.GetAddonByName("FadeMiddle");
        if (addon2->IsVisible) return false;
        if (ActionManager.Instance()->GetActionStatus(ActionType.GeneralAction, 9) != 0) return true;
        if (AgentMap.Instance()->IsPlayerMoving == 1) return true;
        if (Service.Condition[ConditionFlag.Casting] | Service.Condition[ConditionFlag.Casting87]) return true;
        if (Service.Condition[ConditionFlag.Mounted] || Service.Condition[ConditionFlag.Mounted2]) return true;

        TaskManager.Enqueue(() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 9));

        return true;
    }

    public override void Uninit()
    {
        Service.ClientState.TerritoryChanged -= OnZoneChanged;
        Service.Condition.ConditionChange -= OnConditionChanged;
        Service.Toast.ErrorToast -= OnErrorToast;

        base.Uninit();
    }
}
