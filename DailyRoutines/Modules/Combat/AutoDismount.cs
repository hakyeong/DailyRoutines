using System.Collections.Generic;
using System.Linq;
using DailyRoutines.Helpers;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoDismountTitle", "AutoDismountDescription", ModuleCategories.战斗)]
public unsafe class AutoDismount : DailyModuleBase
{
    private static HashSet<uint>? TargetSelfOrAreaActions;

    [Signature("E8 ?? ?? ?? ?? 44 0F B6 C3 48 8B D0")]
    private static delegate* unmanaged<ulong, GameObject*> GetGameObjectFromObjectID;

    public override void Init()
    {
        Service.Hook.InitializeFromAttributes(this);

        TargetSelfOrAreaActions ??=
            PresetData.PlayerActions.Where(x => x.Value.CanTargetSelf || x.Value.TargetArea).Select(x => x.Key).ToHashSet();

        TaskHelper ??= new TaskHelper { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = false };

        Service.UseActionManager.Register(OnUseAction);
    }

    private void OnUseAction(bool result, ActionType actionType, uint actionID, ulong targetID, uint a4, uint queueState, uint a6)
    {
        if (!Flags.IsOnMount) return;
        if (!IsNeedToDismount((uint)actionType, actionID, targetID)) return;

        Service.ExecuteCommandManager.ExecuteCommand(ExecuteCommandFlag.Dismount);
        Service.ClientState.LocalPlayer.ToCharacterStruct()->Mount.CreateAndSetupMount(0, 0, 0, 0, 0, 0, 0);
        Service.ExecuteCommandManager.ExecuteCommand(ExecuteCommandFlag.Dismount);

        TaskHelper.Enqueue(() => Service.ExecuteCommandManager.ExecuteCommand(ExecuteCommandFlag.Dismount));
        TaskHelper.Enqueue(() => ActionManager.Instance()->UseAction(actionType, actionID, targetID, a4, queueState, a6));
    }

    private static bool IsNeedToDismount(uint actionType, uint actionId, ulong actionTarget)
    {
        // 使用的技能是坐骑
        if ((ActionType)actionType == ActionType.Mount) return false;

        var actionManager = ActionManager.Instance();

        // 0 - 该技能无须下坐骑
        if (actionManager->GetActionStatus((ActionType)actionType, actionId, actionTarget, false, false) == 0) return false;

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
}
