using System;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud;

namespace DailyRoutines.Modules;

// 完全来自 Dalamud.SkipCutScene
[ModuleDescription("AutoSkipPraetoriumTitle", "AutoSkipPraetoriumDescription", ModuleCategories.CombatExpand)]
public class AutoSkipPraetorium : DailyModuleBase
{
    public bool Valid => Offset1 != IntPtr.Zero && Offset2 != IntPtr.Zero;
    public nint Offset1 { get; private set; }
    public nint Offset2 { get; private set; }

    public override void Init()
    {
        Offset1 = Service.SigScanner.ScanText
            ("75 33 48 8B 0D ?? ?? ?? ?? BA ?? 00 00 00 48 83 C1 10 E8 ?? ?? ?? ?? 83 78");
        Offset2 = Service.SigScanner.ScanText("74 18 8B D7 48 8D 0D");

        SetEnabled(Valid);
    }

    public void SetEnabled(bool isEnable)
    {
        if (!Valid) return;

        var value1 = isEnable ? (short)-28528 : (short)13173;
        var value2 = isEnable ? (short)-28528 : (short)6260;

        SafeMemory.Write(Offset1, value1);
        SafeMemory.Write(Offset2, value2);
    }

    public override void Uninit()
    {
        if (Initialized) SetEnabled(false);

        base.Uninit();
    }
}
