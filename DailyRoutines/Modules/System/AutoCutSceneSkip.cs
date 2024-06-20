using ClickLib;
using DailyRoutines.Helpers;
using DailyRoutines.Managers;
using Dalamud;
using Dalamud.Hooking;
using Dalamud.Memory;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoCutSceneSkipTitle", "AutoCutSceneSkipDescription", ModuleCategories.系统)]
public class AutoCutSceneSkip : DailyModuleBase
{
    private const string ConditionSig = "75 11 BA ?? ?? ?? ?? 48 8B CF E8 ?? ?? ?? ?? 84 C0 74 52";
    private static nint ConditionAddress;

    private delegate void CutsceneHandleInputDelegate(nint a1);
    [Signature("40 53 48 83 EC 20 80 79 29 00 48 8B D9 0F 85", DetourName = nameof(CutsceneHandleInputDetour))]
    private static Hook<CutsceneHandleInputDelegate>? CutsceneHandleInputHook;

    private delegate nint GetCutSceneRowDelegate(uint row);
    [Signature("E8 ?? ?? ?? ?? 48 85 C0 0F 84 ?? ?? ?? ?? F6 40 ?? ?? 75 ?? BA", DetourName = nameof(GetCutSceneRowDetour))]
    private static Hook<GetCutSceneRowDelegate>? GetCutSceneRowHook;

    private static uint CurrentCutscene;
    private static bool ProhibitSkippingUnseenCutscene;

    public override void Init()
    {
        if (Service.SigScanner.TryScanText(
                "0F B6 D3 48 8D 4C 24 ?? E8 ?? ?? ?? ?? 48 8D 4C 24 ?? E8 ?? ?? ?? ?? 48 8B 5C 24 ?? B8 ?? ?? ?? ?? 48 83 C4 ?? 5F C3 CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC",
                out var ptr))
        {

        }

        Service.Hook.InitializeFromAttributes(this);
        ConditionAddress = Service.SigScanner.ScanText(ConditionSig);
        CutsceneHandleInputHook?.Enable();
        GetCutSceneRowHook?.Enable();

        AddConfig(nameof(ProhibitSkippingUnseenCutscene), false);
        ProhibitSkippingUnseenCutscene = GetConfig<bool>(nameof(ProhibitSkippingUnseenCutscene));

        TaskHelper ??= new TaskHelper { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = false };
    }

    public override void ConfigUI()
    {
        if (ImGui.Checkbox(Service.Lang.GetText("AutoCutSceneSkip-ProhibitSkippingUnseenCutscene"),
                           ref ProhibitSkippingUnseenCutscene))
            UpdateConfig(nameof(ProhibitSkippingUnseenCutscene), ProhibitSkippingUnseenCutscene);

        ImGuiOm.HelpMarker(Service.Lang.GetText("AutoCutSceneSkip-ProhibitSkippingUnseenCutsceneHelp"));
    }


    private unsafe void CutsceneHandleInputDetour(nint a1)
    {
        if (ProhibitSkippingUnseenCutscene && CurrentCutscene != 0)
        {
            if (LuminaCache.GetRow<CutsceneWorkIndex>(CurrentCutscene).WorkIndex != 0 &&
                !UIState.Instance()->IsCutsceneSeen(CurrentCutscene))
            {
                CutsceneHandleInputHook.Original(a1);
                return;
            }
        }

        var allowSkip = *(nint*)(a1 + 56) != 0;
        if (allowSkip)
        {
            SafeMemory.WriteBytes(ConditionAddress, [0xEB]);
            CutsceneHandleInputHook.Original(a1);
            SafeMemory.WriteBytes(ConditionAddress, [0x75]);

            TaskHelper.Enqueue(() =>
            {
                if (!TryGetAddonByName<AtkUnitBase>("SelectString", out var addon) || !IsAddonAndNodesReady(addon))
                    return false;

                if (addon->GetTextNodeById(2)->
                        NodeText.ExtractText().Contains(LuminaCache.GetRow<Addon>(281).Text.RawString))
                {
                    if (Click.TrySendClick("select_string1"))
                    {
                        TaskHelper.Abort();
                        return true;
                    }

                    return false;
                }

                return false;
            });

            return;
        }

        CutsceneHandleInputHook.Original(a1);
    }

    private static nint GetCutSceneRowDetour(uint row)
    {
        CurrentCutscene = row;
        return GetCutSceneRowHook.Original(row);
    }
}
