using DailyRoutines.Managers;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace DailyRoutines.Modules;

[ModuleDescription("BlockInputWhenFishingTitle", "BlockInputWhenFishingDescription", ModuleCategories.系统)]
public unsafe class BlockInputWhenFishing : DailyModuleBase
{
    private delegate bool IsKeyDownDelegate(UIInputData* data, int id);
    [Signature("E8 ?? ?? ?? ?? 84 C0 0F 84 ?? ?? ?? ?? BA ?? ?? ?? ?? 48 8B CE E8 ?? ?? ?? ?? 84 C0 0F 84", 
               DetourName = nameof(IsKeyDownDetour))]
    private static Hook<IsKeyDownDelegate>? IsKeyDownHook;

    public override void Init()
    {
        Service.Hook.InitializeFromAttributes(this);
        Service.Condition.ConditionChange += OnConditionChanged;

        if (Service.Condition[ConditionFlag.Gathering]) IsKeyDownHook.Enable();
    }

    public override void ConfigUI()
    {
        ConflictKeyText();
    }

    private static void OnConditionChanged(ConditionFlag flag, bool isSet)
    {
        if (flag != ConditionFlag.Gathering) return;

        if (isSet) IsKeyDownHook.Enable();
        else IsKeyDownHook.Disable();
    }

    private static bool IsKeyDownDetour(UIInputData* data, int id) 
        => Service.KeyState[Service.Config.ConflictKey] && IsKeyDownHook.Original(data, id);

    public override void Uninit()
    {
        Service.Condition.ConditionChange -= OnConditionChanged;

        base.Uninit();
    }
}
