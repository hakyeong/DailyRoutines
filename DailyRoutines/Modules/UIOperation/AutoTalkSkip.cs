using ClickLib;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoTalkSkipTitle", "AutoTalkSkipDescription", ModuleCategories.界面操作)]
public class AutoTalkSkip : DailyModuleBase
{
    public override void Init() { Service.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, "Talk", OnAddonDraw); }

    public override void ConfigUI() { ConflictKeyText(); }

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
