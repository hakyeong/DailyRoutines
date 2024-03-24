using ClickLib;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoTalkSkipTitle", "AutoTalkSkipDescription", ModuleCategories.Base)]
public class AutoTalkSkip : DailyModuleBase
{
    public override void Init()
    {
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, "Talk", OnAddonDraw);
    }

    public override void ConfigUI() => ConflictKeyText();

    private static void OnAddonDraw(AddonEvent type, AddonArgs args)
    {
        if (Service.KeyState[Service.Config.ConflictKey]) return;
        Click.SendClick("talk");
    }

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddonDraw);

        base.Uninit();
    }
}
