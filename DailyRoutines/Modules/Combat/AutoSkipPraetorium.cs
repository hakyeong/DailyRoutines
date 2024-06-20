using DailyRoutines.Managers;
using Dalamud;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoSkipPraetoriumTitle", "AutoSkipPraetoriumDescription", ModuleCategories.战斗)]
public class AutoSkipPraetorium : DailyModuleBase
{
    private static nint    Ptr       { get; set; }
    private static byte[]? OrigBytes { get; set; }

    public override void Init()
    {
        if (Service.SigScanner.TryScanText("0F B6 D3 48 8D 4C 24 ?? E8 ?? ?? ?? ?? 48 8D 4C 24 ?? E8 ?? ?? ?? ?? 48 8B 5C 24 ?? B8 ?? ?? ?? ?? 48 83 C4 ?? 5F C3 CC CC CC CC CC CC CC CC CC CC CC CC 48 83 EC", out var ptr))
            Ptr = ptr;

        SetEnabled(true);
    }

    private static void SetEnabled(bool isEnabled)
    {
        if (Ptr == nint.Zero) return;

        if (isEnabled)
        {
            if (SafeMemory.ReadBytes(Ptr, 3, out var origBytes))
                OrigBytes ??= origBytes;

            // movzx, edx, bl -> xor edx, edx, nop
            SafeMemory.WriteBytes(Ptr, [0x31, 0xD2, 0x90]);
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

