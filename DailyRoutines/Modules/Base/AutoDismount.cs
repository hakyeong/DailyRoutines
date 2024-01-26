using System;
using System.Numerics;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoDismountTitle", "AutoDismountDescription", ModuleCategories.Base)]
public class AutoDismount : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithUI => false;

    private const string UseActionSig = "E8 ?? ?? ?? ?? EB 64 B1 01 ?? ?? ?? ?? ?? ?? ??";

    private delegate nint UseActionSelfDelegate(long a1, uint a2, uint a3, long a4, int a5, int a6, int a7, byte[] a8);

    private Hook<UseActionSelfDelegate>? useActionSelfHook;

    private static TaskManager? TaskManager;

    public void Init()
    {
        var useActionSelfPtr = Service.SigScanner.ScanText(UseActionSig);
        useActionSelfHook = Hook<UseActionSelfDelegate>.FromAddress(useActionSelfPtr, UseActionSelf);
        useActionSelfHook.Enable();

        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = false };
    }

    public void UI() { }

    private unsafe nint UseActionSelf(long actionManager, uint actionType, uint actionId, long actionTarget, int a5, int actionReleased, int a7, byte[] a8)
    {
        try
        {
            if (P.PluginInterface.IsDev)
                Service.Log.Debug($"技能类型: {(ActionType)actionType} 技能ID: {actionId} 技能目标ID: {actionTarget:X}");

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
        // 技能必须要有目标
        if (actionRange != 0)
        {
            // 但是当前没有目标
            if (Service.Target.Target == null) return false;
            // 对非自身的目标使用技能
            if (actionTarget != 3758096384L)
            {
                // 目标在技能射程之外
                if (CalculateDistance(Service.ClientState.LocalPlayer.Position, Service.Target.Target.Position) - 2 > actionRange) return false;
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

    private static float CalculateDistance(Vector3 vector1, Vector3 vector2)
    {
        var deltaX = vector1.X - vector2.X;
        var deltaY = vector1.Y - vector2.Y;
        var deltaZ = vector1.Z - vector2.Z;

        return (float)Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY) + (deltaZ * deltaZ));
    }

    public void Uninit()
    {
        useActionSelfHook.Dispose();
        TaskManager?.Abort();
    }
}
