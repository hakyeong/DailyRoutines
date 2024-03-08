using ClickLib;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoCutSceneSkipTitle", "AutoCutSceneSkipDescription", ModuleCategories.Base)]
public class AutoCutSceneSkip : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithConfigUI => false;

    private delegate void CutsceneHandleInputDelegate(nint a1);

    [Signature("40 53 48 83 EC 20 80 79 29 00 48 8B D9 0F 85", DetourName = nameof(CutsceneHandleInputDetour))]
    private static Hook<CutsceneHandleInputDelegate>? CutsceneHandleInputHook;

    private const string ConditionSig = "75 11 BA ?? ?? ?? ?? 48 8B CF E8 ?? ?? ?? ?? 84 C0 74 52";
    private static int ConditionOriginalValuesLen => ConditionSig.Split(" ").Length;
    private static nint ConditionAddress;

    public void Init()
    {
        Service.Hook.InitializeFromAttributes(this);
        ConditionAddress = Service.SigScanner.ScanText(ConditionSig);
        CutsceneHandleInputHook?.Enable();

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "SelectString", OnAddon);
    }

    public void ConfigUI() { }

    public void OverlayUI() { }

    private static unsafe void OnAddon(AddonEvent type, AddonArgs args)
    {
        if (TryGetAddonByName<AtkUnitBase>("SelectString", out var addon) && HelpersOm.IsAddonAndNodesReady(addon))
        {
            if (addon->GetTextNodeById(2)->NodeText.ExtractText().Contains("要跳过这段过场动画吗"))
                Click.SendClick("select_string1");
        }
    }

    private static unsafe void CutsceneHandleInputDetour(nint a1)
    {
        var allowSkip = *(nint*)(a1 + 56) != 0;
        if (allowSkip)
        {
            SafeMemory.WriteBytes(ConditionAddress, [0xEB]);
            CutsceneHandleInputHook.Original(a1);
            SafeMemory.WriteBytes(ConditionAddress, [0x75]);

            return;
        }

        CutsceneHandleInputHook.Original(a1);
    }

    public void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddon);
        CutsceneHandleInputHook?.Dispose();
    }
}
