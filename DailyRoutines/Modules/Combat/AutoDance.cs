using System.Collections.Generic;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using ImGuiNET;
using TaskManager = ECommons.Automation.TaskManager;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoDanceTitle", "AutoDanceDescription", ModuleCategories.Combat)]
public unsafe class AutoDance : DailyModuleBase
{
    private delegate bool UseActionSelfDelegate(
        ActionManager* actionManager, uint actionType, uint actionID, ulong targetID = 0xE000_0000, uint a4 = 0,
        uint a5 = 0, uint a6 = 0, void* a7 = null);
    private Hook<UseActionSelfDelegate>? useActionSelfHook;

    private static readonly Dictionary<int, uint> StepActions = new()
    {
        { 1, 15989 },
        { 2, 15990 },
        { 3, 15991 },
        { 4, 15992 }
    };

    private static bool IsAutoFinish;

    public override void Init()
    {
        useActionSelfHook =
            Service.Hook.HookFromAddress<UseActionSelfDelegate>((nint)ActionManager.MemberFunctionPointers.UseAction,
                                                                UseActionSelf);
        useActionSelfHook?.Enable();

        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = false };

        AddConfig(this, "IsAutoFinish", IsAutoFinish);
        IsAutoFinish = GetConfig<bool>(this, "IsAutoFinish");
    }

    public override void ConfigUI()
    {
        if (ImGui.Checkbox(Service.Lang.GetText("AutoDance-AutoFinish"), ref IsAutoFinish))
        {
            UpdateConfig(this, "IsAutoFinish", IsAutoFinish);
        }
    }

    private bool UseActionSelf(
        ActionManager* actionManager, uint actionType, uint actionID, ulong targetID, uint a4, uint a5, uint a6,
        void* a7)
    {
        if ((ActionType)actionType is ActionType.Action && actionID is 15997 or 15998)
        {
            TaskManager.DelayNext(250);
            TaskManager.Enqueue(actionID == 15997 ? DanceStandardStep : DanceTechnicalStep);
        }

        return useActionSelfHook.Original(actionManager, actionType, actionID, targetID, a4, a5, a6, a7);
    }

    private bool? DanceStandardStep() => DanceStep(false);
    private bool? DanceTechnicalStep() => DanceStep(true);

    private bool? DanceStep(bool isTechnicalStep)
    {
        if (TryGetAddonByName<AddonJobHudDNC0>("JobHudDNC0", out var addon))
        {
            var completedSteps = addon->DataCurrent.CompletedSteps;
            var allSteps = addon->DataCurrent.Steps;
            var currentStep = *(allSteps + completedSteps);

            if (completedSteps < (isTechnicalStep ? 4 : 2))
            {
                if (!StepActions.TryGetValue(currentStep, out var nextAction))
                {
                    TaskManager.Abort();
                    return true;
                }

                if (ActionManager.Instance()->UseAction(ActionType.Action, nextAction))
                {
                    TaskManager.DelayNext(250);
                    TaskManager.Enqueue(() => DanceStep(isTechnicalStep));
                    return true;
                }
            }
            else
            {
                if (IsAutoFinish)
                {
                    TaskManager.DelayNext(250);
                    TaskManager.Enqueue(() => ActionManager.Instance()->UseAction(ActionType.Action, isTechnicalStep ? 15998U : 15997U));
                }
                return true;
            }

        }

        return false;
    }
}
