using System;
using DailyRoutines.Managers;
using Dalamud;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace DailyRoutines.Modules;

// 完全来自 Dalamud.SkipCutScene
[ModuleDescription("AutoSkipPraetoriumTitle", "AutoSkipPraetoriumDescription", ModuleCategories.战斗)]
public class AutoSkipPraetorium : DailyModuleBase
{
    public static bool Valid   => Offset1 != nint.Zero && Offset2 != nint.Zero;
    public static nint Offset1 { get; private set; }
    public static nint Offset2 { get; private set; }

    private unsafe delegate bool IsCutsceneSeenDelegate(PlayerState* instance, uint a2);
    [Signature(
        "E8 ?? ?? ?? ?? 33 C9 0F B6 DB 3C ?? 0F 44 D9 0F B6 D3 48 8D 4C 24 ?? E8 ?? ?? ?? ?? 48 8D 4C 24 ?? E8 ?? ?? ?? ?? 48 8B 5C 24 ?? B8 ?? ?? ?? ?? 48 83 C4 ?? 5F C3 CC CC CC CC CC CC CC CC CC CC CC CC 48 83 EC",
        DetourName = nameof(IsCutsceneSeenDetour))]
    private static Hook<IsCutsceneSeenDelegate>? IsCutsceneSeenHook;

    public override void Init()
    {
        Service.Hook.InitializeFromAttributes(this);
        IsCutsceneSeenHook?.Enable();

        Offset1 = Service.SigScanner.ScanText
            ("75 33 48 8B 0D ?? ?? ?? ?? BA ?? 00 00 00 48 83 C1 10 E8 ?? ?? ?? ?? 83 78");
        Offset2 = Service.SigScanner.ScanText("74 18 8B D7 48 8D 0D");

        SetEnabled(Valid);
    }

    public static void SetEnabled(bool isEnable)
    {
        if (!Valid) return;

        var value1 = isEnable ? (short)-28528 : (short)13173;
        var value2 = isEnable ? (short)-28528 : (short)6260;

        SafeMemory.Write(Offset1, value1);
        SafeMemory.Write(Offset2, value2);
    }

    private static unsafe bool IsCutsceneSeenDetour(PlayerState* instance, uint a2) { return true; }

    public override void Uninit()
    {
        if (Initialized) SetEnabled(false);

        base.Uninit();
    }
}
