using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoHolmgangSelfTitle", "AutoHolmgangSelfDescription", ModuleCategories.¼¼ÄÜ)]
public unsafe class AutoHolmgangSelf : DailyModuleBase
{
    private delegate bool UseActionSelfDelegate(
        ActionManager* actionManager, uint actionType, uint actionID, ulong targetID = 0xE000_0000, uint a4 = 0,
        uint a5 = 0, uint a6 = 0, void* a7 = null);

    private Hook<UseActionSelfDelegate>? useActionSelfHook;

    public override void Init()
    {
        useActionSelfHook =
            Service.Hook.HookFromAddress<UseActionSelfDelegate>((nint)ActionManager.MemberFunctionPointers.UseAction,
                                                                UseActionSelf);
        useActionSelfHook?.Enable();
    }

    private bool UseActionSelf(
        ActionManager* actionManager, uint actionType, uint actionID, ulong targetID, uint a4, uint a5, uint a6,
        void* a7)
    {
        if (actionType != 1 || actionID != 43)
            return useActionSelfHook.Original(actionManager, actionType, actionID, targetID, a4, a5, a6, a7);

        return useActionSelfHook.Original(actionManager, actionType, actionID, 3758096384UL, a4, a5, a6, a7);
    }
}
