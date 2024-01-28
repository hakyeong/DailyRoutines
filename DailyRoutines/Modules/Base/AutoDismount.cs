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
public class AutoDismount : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithUI => false;

    private delegate nint UseActionSelfDelegate(long a1, uint a2, uint a3, long a4, int a5, int a6, int a7, byte[] a8);

    [Signature("E8 ?? ?? ?? ?? EB 64 B1 01 ?? ?? ?? ?? ?? ?? ??", DetourName = nameof(UseActionSelf))]
    private Hook<UseActionSelfDelegate>? useActionSelfHook;

    private static HashSet<uint>? CanTargetSelfActions;
    private static TaskManager? TaskManager;

    public void Init()
    {
        SignatureHelper.Initialise(this);
        useActionSelfHook?.Enable();
        CanTargetSelfActions ??= Service.ExcelData.Actions.Where(x => x.Value.CanTargetSelf).Select(x => x.Key).ToHashSet();

        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = false };
    }

    public void UI() { }

    private unsafe nint UseActionSelf(long actionManager, uint actionType, uint actionId, long actionTarget, int a5, int actionReleased, int a7, byte[] a8)
    {
        try
        {
            TaskManager.Abort();
            if (IsNeedToDismount(actionType, actionId, actionTarget))
            {
                TaskManager.Enqueue(() => useActionSelfHook.Original(actionManager, 13, 99999, actionTarget, a5, actionReleased, a7, a8));
                TaskManager.Enqueue(() => ActionManager.Instance()->UseAction((ActionType)actionType, actionId, actionTarget));
            }
        }
        catch (Exception e)
        {
            Service.Log.Warning(e.Message);
            Service.Log.Warning(e.StackTrace ?? "Unknown");
        }

        return useActionSelfHook.Original(actionManager, actionType, actionId, actionTarget, a5, actionReleased, a7, a8);
    }

    private static unsafe bool IsNeedToDismount(uint actionType, uint actionId, long actionTarget)
    {
        // 根本不在坐骑上
        if (!IsOnMount()) return false;
        // 使用的技能是坐骑
        if ((ActionType)actionType == ActionType.Mount) return false;

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

        var actionStatus = ActionManager.Instance()->GetActionStatus((ActionType)actionType, actionId, actionTarget);

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
