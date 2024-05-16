using DailyRoutines.Infos;
using DailyRoutines.Managers;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace DailyRoutines.Modules;

[ModuleDescription("MacroIntoActionQueueTitle", "MacroIntoActionQueueDescription", ModuleCategories.技能)]
public class MacroIntoActionQueue : DailyModuleBase
{
    public override void Init()
    {
        Service.UseActionManager.Register(OnPreUseAction);
    }

    private static void OnPreUseAction(ref bool isPrevented, ref ActionType actionType, ref uint actionID, ref ulong targetID, ref uint a4, ref uint queueState, ref uint a6)
    {
        // 宏
        if (queueState is 0 or 2) queueState = 1;
        // 冲刺
        if (actionType == ActionType.GeneralAction && actionID == 4)
        {
            actionType = ActionType.Action;
            actionID = 3;
            targetID = 0xE000_0000;
        }
    }

    public override void Uninit()
    {
        Service.UseActionManager.Unregister(OnPreUseAction);

        base.Uninit();
    }
}
