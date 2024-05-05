using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoEnableAttackTitle", "AutoEnableAttackDescription", ModuleCategories.Action)]
public class AutoEnableAttack : DailyModuleBase
{
    private nint AutoAttackAddress = nint.Zero;
    private byte[] AutoAttackOriginalBytes = new byte[12];

    private readonly byte[] AutoAttackOverwriteBytes =
        [0x41, 0xF6, 0x47, 0x39, 0x04, 0x0F, 0x85, 0xA7, 0x00, 0x00, 0x00, 0x90];

    public override void Init()
    {
        AutoAttackAddress = Service.SigScanner.ScanText("41 B0 01 41 0F B6 D0 E9 ?? ?? ?? ?? 41 B0 01");

        if (SafeMemory.ReadBytes(AutoAttackAddress, 12, out AutoAttackOriginalBytes))
            SafeMemory.WriteBytes(AutoAttackAddress, AutoAttackOverwriteBytes);
    }

    public override void Uninit()
    {
        if (AutoAttackAddress != nint.Zero) SafeMemory.WriteBytes(AutoAttackAddress, AutoAttackOriginalBytes);

        base.Uninit();
    }
}
