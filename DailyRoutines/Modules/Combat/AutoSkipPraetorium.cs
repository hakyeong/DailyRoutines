using System.Collections.Generic;
using DailyRoutines.Managers;
using Dalamud;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace DailyRoutines.Modules;

// 主要逻辑来自 Dalamud.SkipCutScene
[ModuleDescription("AutoSkipPraetoriumTitle", "AutoSkipPraetoriumDescription", ModuleCategories.战斗)]
public class AutoSkipPraetorium : DailyModuleBase
{
    private static readonly nint Offset1 = Service.SigScanner.ScanText
        ("75 33 48 8B 0D ?? ?? ?? ?? BA ?? 00 00 00 48 83 C1 10 E8 ?? ?? ?? ?? 83 78");
    private static readonly nint Offset2 = Service.SigScanner.ScanText
        ("74 18 8B D7 48 8D 0D");

    private static readonly HashSet<uint> Zones = [1043, 1044, 1048];
    private const short EnableValue = -28528;
    private const short DisableValue = 13173;
    private const short DisableValue2 = 6260;

    private unsafe delegate bool IsCutsceneSeenDelegate(PlayerState* instance, uint a2);
    [Signature("E8 ?? ?? ?? ?? 33 C9 0F B6 DB 3C ?? 0F 44 D9 0F B6 D3 48 8D 4C 24 ?? E8 ?? ?? ?? ?? 48 8D 4C 24 ?? E8 ?? ?? ?? ?? 48 8B 5C 24 ?? B8 ?? ?? ?? ?? 48 83 C4 ?? 5F C3 CC CC CC CC CC CC CC CC CC CC CC CC 48 83 EC", DetourName = nameof(IsCutsceneSeenDetour))]
    private static Hook<IsCutsceneSeenDelegate>? IsCutsceneSeenHook;

    public override void Init()
    {
        Service.Hook.InitializeFromAttributes(this);
        IsCutsceneSeenHook?.Enable();
        SetEnabled(IsOffsetsValid());
    }

    private static bool IsOffsetsValid() => Offset1 != nint.Zero && Offset2 != nint.Zero;

    private static void SetEnabled(bool isEnabled)
    {
        if (!IsOffsetsValid()) return;

        SafeMemory.Write(Offset1, isEnabled ? EnableValue : DisableValue);
        SafeMemory.Write(Offset2, isEnabled ? EnableValue : DisableValue2);
    }

    private static unsafe bool IsCutsceneSeenDetour(PlayerState* instance, uint a2)
        => Zones.Contains(Service.ClientState.TerritoryType) || IsCutsceneSeenHook.Original(instance, a2);

    public override void Uninit()
    {
        SetEnabled(false);

        base.Uninit();
    }
}

