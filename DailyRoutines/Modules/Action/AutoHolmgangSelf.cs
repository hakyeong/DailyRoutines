using DailyRoutines.Infos;
using DailyRoutines.Managers;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoHolmgangSelfTitle", "AutoHolmgangSelfDescription", ModuleCategories.技能)]
public class AutoHolmgangSelf : DailyModuleBase
{
    public override void Init()
    {
        Service.UseActionManager.Register(OnPreUseAction);
    }

    private static void OnPreUseAction(ref bool isPrevented, ref ActionType actionType, ref uint actionID, ref ulong targetID, ref uint a4, ref uint queueState, ref uint a6)
    {
        if (actionType is ActionType.Action && actionID is 43) targetID = 0xE0000000UL;
    }

    public override void Uninit()
    {
        Service.UseActionManager.Unregister(OnPreUseAction);

        base.Uninit();
    }
}
