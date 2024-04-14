using ClickLib;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoCutSceneSkipTitle", "AutoCutSceneSkipDescription", ModuleCategories.Base)]
public class AutoCutSceneSkip : DailyModuleBase
{
    private delegate void CutsceneHandleInputDelegate(nint a1);

    [Signature("40 53 48 83 EC 20 80 79 29 00 48 8B D9 0F 85", DetourName = nameof(CutsceneHandleInputDetour))]
    private readonly Hook<CutsceneHandleInputDelegate>? CutsceneHandleInputHook;

    private const string ConditionSig = "75 11 BA ?? ?? ?? ?? 48 8B CF E8 ?? ?? ?? ?? 84 C0 74 52";
    private static int ConditionOriginalValuesLen => ConditionSig.Split(" ").Length;
    private static nint ConditionAddress;

    public override void Init()
    {
        Service.Hook.InitializeFromAttributes(this);
        ConditionAddress = Service.SigScanner.ScanText(ConditionSig);
        CutsceneHandleInputHook?.Enable();

        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = false };
    }

    private unsafe void CutsceneHandleInputDetour(nint a1)
    {
        var allowSkip = *(nint*)(a1 + 56) != 0;
        if (allowSkip)
        {
            SafeMemory.WriteBytes(ConditionAddress, [0xEB]);
            CutsceneHandleInputHook.Original(a1);
            SafeMemory.WriteBytes(ConditionAddress, [0x75]);

            TaskManager.Enqueue(() =>
            {
                if (TryGetAddonByName<AtkUnitBase>("SelectString", out var addon) && IsAddonAndNodesReady(addon))
                {
                    if (addon->GetTextNodeById(2)->NodeText.ExtractText().Contains("要跳过这段过场动画吗"))
                        return Click.TrySendClick("select_string1");
                }

                return false;
            });

            return;
        }

        CutsceneHandleInputHook.Original(a1);
    }
}
