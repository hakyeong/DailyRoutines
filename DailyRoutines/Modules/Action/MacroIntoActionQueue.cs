using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace DailyRoutines.Modules;

[ModuleDescription("MacroIntoActionQueueTitle", "MacroIntoActionQueueDescription", ModuleCategories.技能)]
public unsafe class MacroIntoActionQueue : DailyModuleBase
{
    private delegate bool UseActionMacroDelegate(ActionManager* actionManager, ActionType actionType, uint actionID, ulong targetID = 0xE000_0000, uint a4 = 0, uint a5 = 0, uint a6 = 0, void* a7 = null);
    [Signature("E8 ?? ?? ?? ?? 80 7C 24 ?? ?? 44 0F B6 E8", DetourName = nameof(UseActionMacroDetour))]
    private readonly Hook<UseActionMacroDelegate>? UseActionMacroHook;

    public override void Init()
    {
        Service.Hook.InitializeFromAttributes(this);
        UseActionMacroHook?.Enable();
    }

    private bool UseActionMacroDetour(ActionManager* actionManager, ActionType actionType, uint actionID, ulong targetID, uint a4, uint queueState, uint a6, void* a7)
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
        return UseActionMacroHook.Original(actionManager, actionType, actionID, targetID, a4, queueState, a6, a7);
    }
}
