using System;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud;
using Dalamud.Game;

namespace DailyRoutines.Modules;

// 完全来自 Dalamud.SkipCutScene
[ModuleDescription("AutoSkipPraetoriumTitle", "AutoSkipPraetoriumDescription", ModuleCategories.Combat)]
public class AutoSkipPraetorium : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithConfigUI => false;
    public CutsceneAddressResolver? Address { get; set; }

    public void Init()
    {
        Address = new CutsceneAddressResolver();
        Address.Setup(Service.SigScanner);
        SetEnabled(Address.Valid);
    }

    public void SetEnabled(bool isEnable)
    {
        if (!Address.Valid) return;

        var value1 = isEnable ? (short)-28528 : (short)13173;
        var value2 = isEnable ? (short)-28528 : (short)6260;

        SafeMemory.Write(Address.Offset1, value1);
        SafeMemory.Write(Address.Offset2, value2);
    }

    public void ConfigUI() { }

    public void OverlayUI() { }

    public void Uninit()
    {
        if (Initialized) SetEnabled(false);
    }
}

public class CutsceneAddressResolver : BaseAddressResolver
{
    public bool Valid => Offset1 != IntPtr.Zero && Offset2 != IntPtr.Zero;
    public nint Offset1 { get; private set; }
    public nint Offset2 { get; private set; }

    protected override void Setup64Bit(ISigScanner sig)
    {
        Offset1 = sig.ScanText("75 33 48 8B 0D ?? ?? ?? ?? BA ?? 00 00 00 48 83 C1 10 E8 ?? ?? ?? ?? 83 78");
        Offset2 = sig.ScanText("74 18 8B D7 48 8D 0D");
    }
}

