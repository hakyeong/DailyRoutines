using ClickLib;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.AddonLifecycle;
using ECommons.Throttlers;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoTalkSkipTitle", "AutoTalkSkipDescription", ModuleCategories.Base)]
public class AutoTalkSkip : IDailyModule
{
    public bool Initialized { get; set; }

    public void Init()
    {
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, "Talk", OnAddonDraw);

        Initialized = true;
    }

    public void UI() { }

    private static void OnAddonDraw(AddonEvent type, AddonArgs args)
    {
        if (EzThrottler.Throttle("AutoTalkSkip", 50))
        {
            Click.SendClick("talk");
        }
    }

    public void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddonDraw);

        Initialized = false;
    }
}
