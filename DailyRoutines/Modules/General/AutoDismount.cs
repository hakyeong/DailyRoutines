using System.Collections.Generic;
using System.Linq;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoDismountTitle", "AutoDismountDescription", ModuleCategories.General)]
public unsafe class AutoDismount : DailyModuleBase
{
    private delegate bool UseActionSelfDelegate(
        ActionManager* actionManager, uint actionType, uint actionID, ulong targetID = 0xE000_0000, uint a4 = 0,
        uint a5 = 0, uint a6 = 0, void* a7 = null);

    private Hook<UseActionSelfDelegate>? useActionSelfHook;

    [Signature("E8 ?? ?? ?? ?? 44 0F B6 C3 48 8B D0")]
    private readonly delegate* unmanaged<ulong, GameObject*> GetGameObjectFromObjectID;

    private static HashSet<uint>? TargetSelfOrAreaActions;

    public override void Init()
    {
        Service.Hook.InitializeFromAttributes(this);
        useActionSelfHook =
            Service.Hook.HookFromAddress<UseActionSelfDelegate>((nint)ActionManager.MemberFunctionPointers.UseAction,
                                                                UseActionSelf);

        TargetSelfOrAreaActions ??=
            PresetData.PlayerActions.Where(x => x.Value.CanTargetSelf || x.Value.TargetArea).Select(x => x.Key)
                      .ToHashSet();

        Service.Condition.ConditionChange += OnConditionChanged;

        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = false };
    }

    private void OnConditionChanged(ConditionFlag flag, bool value)
    {
        if (flag is ConditionFlag.Mounted or ConditionFlag.Mounted2)
        {
            if (value)
                useActionSelfHook?.Enable();
            else
                useActionSelfHook?.Disable();
        }
    }

    private bool UseActionSelf(
        ActionManager* actionManager, uint actionType, uint actionId, ulong actionTarget, uint a5, uint a6, uint a7,
        void* a8)
    {
        TaskManager.Abort();
        if (IsNeedToDismount(actionType, actionId, actionTarget))
        {
            useActionSelfHook.Original(actionManager, 5, 9, 0);
            TaskManager.Enqueue(
                () => ActionManager.Instance()->UseAction((ActionType)actionType, actionId, actionTarget, a5, a6, a7,
                                                          a8));
        }

        return useActionSelfHook.Original(actionManager, actionType, actionId, actionTarget, a5, a6, a7, a8);
    }

    private bool IsNeedToDismount(uint actionType, uint actionId, ulong actionTarget)
    {
        // 使用的技能是坐骑
        if ((ActionType)actionType == ActionType.Mount) return false;

        var actionManager = ActionManager.Instance();

        // 0 - 该技能无须下坐骑
        if (actionManager->GetActionStatus((ActionType)actionType, actionId, actionTarget, false,
                                           false) == 0) return false;

        // 技能当前不可用
        if (!actionManager->IsActionOffCooldown((ActionType)actionType, actionId)) return false;

        // 可以自身或地面为目标的技能
        if (TargetSelfOrAreaActions.Contains(actionId)) return true;

        var actionRange = ActionManager.GetActionRange(actionId);
        var actionObject = GetGameObjectFromObjectID(actionTarget);
        // 技能必须要有目标
        if (actionRange != 0)
        {
            // 对非自身的目标使用技能
            if (actionTarget != 3758096384L)
            {
                var localPlayer = (GameObject*)Service.ClientState.LocalPlayer.Address;
                // 562 - 看不到目标; 566 - 目标在射程外
                if (ActionManager.GetActionInRangeOrLoS(actionId, localPlayer, actionObject) is 562 or 566)
                    return false;

                // 无法对目标使用技能
                if (!ActionManager.CanUseActionOnTarget(actionId, actionObject)) return false;
            }
            else if (Service.Target.Target == null) return false;
        }

        return true;
    }

    public override void Uninit()
    {
        Service.Condition.ConditionChange -= OnConditionChanged;

        base.Uninit();
    }
}
