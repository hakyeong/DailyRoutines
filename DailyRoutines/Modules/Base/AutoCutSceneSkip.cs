using ClickLib;
using DailyRoutines.Helpers;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoCutSceneSkipTitle", "AutoCutSceneSkipDescription", ModuleCategories.Base)]
public class AutoCutSceneSkip : DailyModuleBase
{
    private delegate void CutsceneHandleInputDelegate(nint a1);
    [Signature("40 53 48 83 EC 20 80 79 29 00 48 8B D9 0F 85", DetourName = nameof(CutsceneHandleInputDetour))]
    private readonly Hook<CutsceneHandleInputDelegate>? CutsceneHandleInputHook;

    private delegate nint GetCutSceneRowDelegate(uint row);
    [Signature("48 83 EC ?? 48 8B 05 ?? ?? ?? ?? 44 8B C1 BA ?? ?? ?? ?? 48 8B 88 ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 85 C0 75 ?? 48 83 C4 ?? C3 48 8B 00 48 83 C4 ?? C3 CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC 48 8B 05 ?? ?? ?? ?? BA ?? ?? ?? ?? 48 8B 88 ?? ?? ?? ?? E9 ?? ?? ?? ?? CC CC CC CC CC CC CC CC 48 83 EC ?? 48 8B 05 ?? ?? ?? ?? 44 8B C1 BA ?? ?? ?? ?? 48 8B 88 ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 85 C0 75 ?? 48 83 C4 ?? C3 48 8B 00 48 83 C4 ?? C3 CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC 48 8B 05 ?? ?? ?? ?? BA ?? ?? ?? ?? 48 8B 88 ?? ?? ?? ?? E9 ?? ?? ?? ?? CC CC CC CC CC CC CC CC 48 83 EC ?? 48 8B 05 ?? ?? ?? ?? 44 8B CA", DetourName = nameof(GetCutSceneRowDetour))]
    private readonly Hook<GetCutSceneRowDelegate>? GetCutSceneRowHook;

    private uint CurrentCutscene;
    private bool ProhibitSkippingUnseenCutscene;

    private const string ConditionSig = "75 11 BA ?? ?? ?? ?? 48 8B CF E8 ?? ?? ?? ?? 84 C0 74 52";
    private static nint ConditionAddress;

    public override void Init()
    {
        Service.Hook.InitializeFromAttributes(this);
        ConditionAddress = Service.SigScanner.ScanText(ConditionSig);
        CutsceneHandleInputHook?.Enable();
        GetCutSceneRowHook?.Enable();

        AddConfig(nameof(ProhibitSkippingUnseenCutscene), false);
        ProhibitSkippingUnseenCutscene = GetConfig<bool>(nameof(ProhibitSkippingUnseenCutscene));

        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = false };
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

            TaskManager.Enqueue(() =>
            {
                if (TryGetAddonByName<AtkUnitBase>("SelectString", out var addon) && IsAddonAndNodesReady(addon))
                {
                    if (addon->GetTextNodeById(2)->NodeText.ExtractText().Contains(LuminaCache.GetRow<Addon>(281).Text.RawString))
                    {
                        var state = Click.TrySendClick("select_string1");
                        if (state)
                        {
                            TaskManager.Abort();
                            return true;
                        }
                        return false;
                    }
                }

                return false;
            });

            return;
        }

        CutsceneHandleInputHook.Original(a1);
    }

    private nint GetCutSceneRowDetour(uint row)
    {
        CurrentCutscene = row;
        return GetCutSceneRowHook.Original(row);
    }
}
