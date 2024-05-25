using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;

namespace DailyRoutines.Modules;

[ModuleDescription("BanEscToCancelCastTitle", "BanEscToCancelCastDescription", ModuleCategories.技能)]
public class BanEscToCancelCast : DailyModuleBase
{
    private delegate bool CheckCastCancelDelegate(nint a1);
    [Signature("40 57 48 83 EC ?? 48 8B F9 48 8B 49 ?? 48 8B 01 FF 50 ?? 48 8B C8 E8 ?? ?? ?? ?? 84 C0 0F 85", DetourName = nameof(CheckCastCancelDetour))]
    private static Hook<CheckCastCancelDelegate>? CheckCastCancelHook;

    public override void Init()
    {
        Service.Hook.InitializeFromAttributes(this);
        CheckCastCancelHook?.Enable();
    }

    private static bool CheckCastCancelDetour(nint _) => true;
}
