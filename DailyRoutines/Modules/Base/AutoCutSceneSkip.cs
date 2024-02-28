using ClickLib;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoCutSceneSkipTitle", "AutoCutSceneSkipDescription", ModuleCategories.Base)]
public class AutoCutSceneSkip : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithConfigUI => false;

    public void Init()
    {
        AutoCutsceneSkipper.Init(null);
        AutoCutsceneSkipper.Enable();

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

    public void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddon);
        AutoCutsceneSkipper.Disable();
    }
}
