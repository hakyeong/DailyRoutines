using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.ClientState.Conditions;
using ECommons.Automation;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Fate;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoMountTitle", "AutoMountDescription", ModuleCategories.General)]
public unsafe class AutoMount : DailyModuleBase
{
    private static AtkUnitBase* NowLoading => (AtkUnitBase*)Service.Gui.GetAddonByName("NowLoading");
    private static AtkUnitBase* FadeMiddle => (AtkUnitBase*)Service.Gui.GetAddonByName("FadeMiddle");

    private static bool MountWhenZoneChange;
    private static bool MountWhenGatherEnd;
    private static bool MountWhenCombatEnd;

    public override void Init()
    {
        #region Config

        AddConfig("MountWhenZoneChange", true);
        MountWhenZoneChange = GetConfig<bool>("MountWhenZoneChange");

        AddConfig("MountWhenGatherEnd", true);
        MountWhenGatherEnd = GetConfig<bool>("MountWhenGatherEnd");

        AddConfig("MountWhenCombatEnd", true);
        MountWhenCombatEnd = GetConfig<bool>("MountWhenCombatEnd");

        #endregion

        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 20000, ShowDebug = false };

        Service.Condition.ConditionChange += OnConditionChanged;
        Service.ClientState.TerritoryChanged += OnZoneChanged;
    }

    public override void ConfigUI()
    {
        if (ImGui.Checkbox(Service.Lang.GetText("AutoMount-MountWhenZoneChange"), ref MountWhenZoneChange))
            UpdateConfig("MountWhenZoneChange", MountWhenZoneChange);

        if (ImGui.Checkbox(Service.Lang.GetText("AutoMount-MountWhenGatherEnd"), ref MountWhenGatherEnd))
            UpdateConfig("MountWhenGatherEnd", MountWhenGatherEnd);

        if (ImGui.Checkbox(Service.Lang.GetText("AutoMount-MountWhenCombatEnd"), ref MountWhenCombatEnd))
            UpdateConfig("MountWhenCombatEnd", MountWhenCombatEnd);
    }

    private void OnZoneChanged(ushort zone)
    {
        if (!MountWhenZoneChange) return;

        TaskManager.Abort();
        TaskManager.Enqueue(UseMountBetweenMap);
    }

    private void OnConditionChanged(ConditionFlag flag, bool value)
    {
        switch (flag)
        {
            case ConditionFlag.Gathering when !value && MountWhenGatherEnd:
            case ConditionFlag.InCombat when !value && MountWhenCombatEnd && !Service.ClientState.IsPvP &&
                                             (FateManager.Instance()->CurrentFate == null ||
                                              FateManager.Instance()->CurrentFate->Progress == 100):
                TaskManager.Abort();

                TaskManager.DelayNext(500);
                TaskManager.Enqueue(UseMountInMap);
                break;
        }
    }

    private bool? UseMountInMap()
    {
        if (!EzThrottler.Throttle("AutoMount")) return false;
        if (AgentMap.Instance()->IsPlayerMoving == 1) return true;
        if (Flags.IsCasting || Flags.IsOnMount) return true;
        if (ActionManager.Instance()->GetActionStatus(ActionType.GeneralAction, 9) != 0) return false;

        TaskManager.DelayNext(100);
        TaskManager.Enqueue(() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 9));
        return true;
    }

    private bool? UseMountBetweenMap()
    {
        if (!EzThrottler.Throttle("AutoMount")) return false;
        if (Service.Condition[ConditionFlag.BetweenAreas]) return false;
        if (NowLoading->IsVisible) return false;

        if (AgentMap.Instance()->IsPlayerMoving == 1) return true;
        if (ActionManager.Instance()->GetActionStatus(ActionType.GeneralAction, 9) != 0) return !FadeMiddle->IsVisible;
        if (Flags.IsCasting || Flags.IsOnMount) return true;

        if (!NowLoading->IsVisible && FadeMiddle->IsVisible)
        {
            TaskManager.DelayNext(100);
            TaskManager.Enqueue(() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 9));
            return true;
        }

        return true;
    }

    public override void Uninit()
    {
        Service.ClientState.TerritoryChanged -= OnZoneChanged;
        Service.Condition.ConditionChange -= OnConditionChanged;

        base.Uninit();
    }
}
