using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace DailyRoutines.Modules;

[ModuleDescription("MacroIntoActionQueueTitle", "MacroIntoActionQueueDescription", ModuleCategories.技能)]
public unsafe class MacroIntoActionQueue : DailyModuleBase
{
    private delegate bool UseActionMacroDelegate(ActionManager* actionManager, uint actionType, uint actionID, ulong targetID = 0xE000_0000, uint a4 = 0, uint a5 = 0, uint a6 = 0, void* a7 = null);
    [Signature("E8 ?? ?? ?? ?? 80 7C 24 ?? ?? 44 0F B6 E8", DetourName = nameof(UseActionMacroDetour))]
    private readonly Hook<UseActionMacroDelegate>? UseActionMacroHook;

    public override void Init()
    {
        Service.Hook.InitializeFromAttributes(this);
        UseActionMacroHook?.Enable();
    }

    private bool UseActionMacroDetour(ActionManager* actionManager, uint actionType, uint actionID, ulong targetID, uint a4, uint a5, uint a6, void* a7)
    {
        if (a5 == 2) a5 = 1;
        return UseActionMacroHook.Original(actionManager, actionType, actionID, targetID, a4, a5, a6, a7);
    }
}
