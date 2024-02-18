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

[ModuleDescription("AutoDismountTitle", "AutoDismountDescription", ModuleCategories.Combat)]
public unsafe class AutoDismount : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithConfigUI => false;

    private delegate bool UseActionSelfDelegate(
        ActionManager* actionManager, uint actionType, uint actionID, ulong targetID = 0xE000_0000, uint a4 = 0,
        uint a5 = 0, uint a6 = 0, void* a7 = null);

    private Hook<UseActionSelfDelegate>? useActionSelfHook;

    [Signature("E8 ?? ?? ?? ?? 44 0F B6 C3 48 8B D0")]
    private readonly delegate* unmanaged<ulong, GameObject*> GetGameObjectFromObjectID;

    private static HashSet<uint>? CanTargetSelfActions;
    private static HashSet<uint>? TargetAreaActions;
    private static TaskManager? TaskManager;

    public void Init()
    {
        SignatureHelper.Initialise(this);
        useActionSelfHook =
            Hook<UseActionSelfDelegate>.FromAddress((nint)ActionManager.MemberFunctionPointers.UseAction,
                                                    UseActionSelf);
        useActionSelfHook?.Enable();

        CanTargetSelfActions ??=
            Service.ExcelData.PlayerActions.Where(x => x.Value.CanTargetSelf).Select(x => x.Key).ToHashSet();
        TargetAreaActions ??= Service.ExcelData.PlayerActions.Where(x => x.Value.TargetArea).Select(x => x.Key).ToHashSet();

        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = false };
    }

    public void ConfigUI() { }

    public void OverlayUI() { }

    private bool UseActionSelf(
        ActionManager* actionManager, uint actionType, uint actionId, ulong actionTarget, uint a5, uint a6, uint a7,
        void* a8)
    {
        TaskManager.Abort();
        if (IsNeedToDismount(actionType, actionId, actionTarget))
        {
            useActionSelfHook.Original(actionManager, 5, 23, 0);
            TaskManager.Enqueue(
                () => ActionManager.Instance()->UseAction((ActionType)actionType, actionId, (long)actionTarget, a5, a6,
                                                          a7, a8));
        }

        return useActionSelfHook.Original(ActionManager.StaticAddressPointers.pInstance, actionType, actionId,
                                          actionTarget, a5, a6, a7, a8);
    }

    private bool IsNeedToDismount(uint actionType, uint actionId, ulong actionTarget)
    {
        // 根本不在坐骑上
        if (!IsOnMount()) return false;

        // 使用的技能是坐骑
        if ((ActionType)actionType == ActionType.Mount) return false;

        // 0 - 该技能无须下坐骑
        if (ActionManager.Instance()->GetActionStatus((ActionType)actionType, actionId, (long)actionTarget, false,
                                                      false) == 0) return false;

        // 地面类技能
        if (TargetAreaActions.Contains(actionId)) return true;

        // 可以自身为目标的技能
        if (CanTargetSelfActions.Contains(actionId)) return true;

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
                if (ActionManager.GetActionInRangeOrLoS(actionId, localPlayer, actionObject) is 562 or 566) return false;
                // 目标在范围外
                if (!HelpersOm.CanUseActionOnObject(localPlayer, actionObject, actionRange)) return false;

                // 无法对目标使用技能
                if (!ActionManager.CanUseActionOnTarget(actionId, actionObject)) return false;
            }
            else if (Service.Target.Target == null) return false;
        }

        return true;
    }

    private static bool IsOnMount() => Service.Condition[ConditionFlag.Mounted] || Service.Condition[ConditionFlag.Mounted2];

    public void Uninit()
    {
        useActionSelfHook?.Dispose();
        TaskManager?.Abort();
    }
}
