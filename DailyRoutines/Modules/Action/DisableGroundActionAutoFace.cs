using DailyRoutines.Managers;
using Dalamud;

namespace DailyRoutines.Modules;

[ModuleDescription("DisableGroundActionAutoFaceTitle", "DisableGroundActionAutoFaceDescription", ModuleCategories.技能)]
public class DisableGroundActionAutoFace : DailyModuleBase
{
    private static nint    Ptr       { get; set; }
    private static byte[]? OrigBytes { get; set; }

    public override void Init()
    {
        if (Service.SigScanner.TryScanText("41 80 7F ?? ?? 74 ?? 49 8D 8E", out var ptr))
            Ptr = ptr;

        SetEnabled(true);
    }

    private static void SetEnabled(bool isEnabled)
    {
        if (Ptr == nint.Zero) return;

        if (isEnabled)
        {
            if (SafeMemory.ReadBytes(Ptr, 5, out var origBytes))
                OrigBytes ??= origBytes;

            // NOP + 短跳转到下一条 if 判断 (SE 不动的话偏移量就不用改)
            SafeMemory.WriteBytes(Ptr, [0x90, 0x90, 0x90, 0x90, 0x90, 0xEB, 0x28]);
        }
        else
        {
            if (OrigBytes == null) return;

            SafeMemory.WriteBytes(Ptr, OrigBytes);
            OrigBytes = null;
        }
    }

    public override void Uninit()
    {
        SetEnabled(false);
    }
}
