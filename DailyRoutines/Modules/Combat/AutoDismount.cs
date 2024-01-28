using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoDismountTitle", "AutoDismountDescription", ModuleCategories.Combat)]
public unsafe class AutoDismount : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithUI => false;

    private delegate bool UseActionSelfDelegate(ActionManager* actionManager, uint actionType, uint actionID, ulong targetID = 0xE000_0000, uint a4 = 0, uint a5 = 0, uint a6 = 0, void* a7 = null);

    [Signature("E8 ?? ?? ?? ?? EB 64 B1 01 ?? ?? ?? ?? ?? ?? ??", DetourName = nameof(UseActionSelf))]
    private Hook<UseActionSelfDelegate>? useActionSelfHook;

    private static HashSet<uint>? CanTargetSelfActions;
    private static HashSet<uint>? TargetAreaActions;
    private static TaskManager? TaskManager;

    public void Init()
    {
        SignatureHelper.Initialise(this);
        useActionSelfHook?.Enable();
        CanTargetSelfActions ??= Service.ExcelData.Actions.Where(x => x.Value.CanTargetSelf).Select(x => x.Key).ToHashSet();
        TargetAreaActions ??= Service.ExcelData.Actions.Where(x => x.Value.TargetArea).Select(x => x.Key).ToHashSet();

        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = false };
    }

    public void UI() { }

    private bool UseActionSelf(ActionManager* actionManager, uint actionType, uint actionId, ulong actionTarget, uint a5, uint a6, uint a7, void* a8)
    {
        TaskManager.Abort();
        if (IsNeedToDismount(actionType, actionId, actionTarget))
        {
            TaskManager.Enqueue(() => useActionSelfHook.Original(actionManager, 13, 99999, actionTarget, a5, a6, a7, a8));
            TaskManager.Enqueue(() => useActionSelfHook.Original(actionManager, actionType, actionId, actionTarget, a5, a6, a7, a8));
        }

        return useActionSelfHook.Original(ActionManager.StaticAddressPointers.pInstance, actionType, actionId, actionTarget, a5, a6, a7, a8);
    }

    private static bool IsNeedToDismount(uint actionType, uint actionId, ulong actionTarget)
    {
        // 根本不在坐骑上
        if (!IsOnMount()) return false;
        // 使用的技能是坐骑
        if ((ActionType)actionType == ActionType.Mount) return false;

        // 地面类技能
        if (TargetAreaActions.Contains(actionId)) return true;

        var actionRange = ActionManager.GetActionRange(actionId);
        // 技能必须要有目标且目标不会是自己
        if (actionRange != 0 && !CanTargetSelfActions.Contains(actionId))
        {
            // 但是当前没有目标
            if (Service.Target.Target == null) return false;
            // 对非自身的目标使用技能
            if (actionTarget != 3758096384L)
            {
                // 目标在技能射程之外
                if (GetTargetDistance(Service.ClientState.LocalPlayer.Position, Service.Target.Target.Position) > actionRange) return false;
                // 无法对目标使用技能
                if (!ActionManager.CanUseActionOnTarget(actionId, (GameObject*)Service.Target.Target.Address)) return false;
                // 看不到目标
                if (ActionManager.GetActionInRangeOrLoS(actionId, (GameObject*)Service.ClientState.LocalPlayer.Address, (GameObject*)Service.Target.Target.Address) != 0) return false;
            }
        }

        var actionStatus = ActionManager.Instance()->GetActionStatus((ActionType)actionType, actionId, (long)actionTarget);

        // 该技能无须下坐骑
        if (actionStatus == 0) return false;
        // 技能正在冷却
        if (actionStatus == 582) return false;

        return true;
    }

    private static bool IsOnMount()
    {
        return Service.Condition[ConditionFlag.Mounted] || Service.Condition[ConditionFlag.Mounted2];
    }

    private static float GetTargetDistance(Vector3 playerPos, Vector3 objPos)
    {
        return MathF.Sqrt(MathF.Pow(playerPos.X - objPos.X, 2) + MathF.Pow(playerPos.Y - objPos.Y, 2)) - 4;
    }

    public void Uninit()
    {
        useActionSelfHook?.Dispose();
        TaskManager?.Abort();
    }
}
