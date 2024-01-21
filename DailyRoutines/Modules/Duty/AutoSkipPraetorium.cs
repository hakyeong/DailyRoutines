using System;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud;
using Dalamud.Game;

namespace DailyRoutines.Modules;

// 完全来自 Dalamud.SkipCutScene
[ModuleDescription("AutoSkipPraetoriumTitle", "AutoSkipPraetoriumDescription", ModuleCategories.Duty)]
public class AutoSkipPraetorium : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithUI => false;
    public CutsceneAddressResolver? Address { get; set; }

    public void Init()
    {
        Address = new CutsceneAddressResolver();
        Address.Setup(Service.SigScanner);
        if (Address.Valid)
            SetEnabled(true);
        else
            Uninit();

        Initialized = true;
    }

    public void SetEnabled(bool isEnable)
    {
        if (!Address.Valid) return;
        if (isEnable)
        {
            SafeMemory.Write<short>(Address.Offset1, -28528);
            SafeMemory.Write<short>(Address.Offset2, -28528);
        }
        else
        {
            SafeMemory.Write<short>(Address.Offset1, 13173);
            SafeMemory.Write<short>(Address.Offset2, 6260);
        }
    }

    public void UI() { }

    public void Uninit()
    {
        if (Initialized)
        {
            SetEnabled(false);
            GC.SuppressFinalize(this);
        }

        Initialized = false;
    }
}

public class CutsceneAddressResolver : BaseAddressResolver
{
    public bool Valid
    {
        get
        {
            if (Offset1 != IntPtr.Zero) return Offset2 != IntPtr.Zero;
            return false;
        }
    }

    public nint Offset1 { get; private set; }

    public nint Offset2 { get; private set; }

    protected override void Setup64Bit(SigScanner sig)
    {
        Offset1 = sig.ScanText("75 33 48 8B 0D ?? ?? ?? ?? BA ?? 00 00 00 48 83 C1 10 E8 ?? ?? ?? ?? 83 78");
        Offset2 = sig.ScanText("74 18 8B D7 48 8D 0D");
    }
}
