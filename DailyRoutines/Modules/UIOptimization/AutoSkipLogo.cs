using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;

namespace DailyRoutines.Modules;

// From HaselTweaks
[ModuleDescription("AutoSkipLogoTitle", "AutoSkipLogoDescription", ModuleCategories.界面优化)]
public class AutoSkipLogo : DailyModuleBase
{
    public override void Init()
    {
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "Logo", OnLogo);
    }

    private static unsafe void OnLogo(AddonEvent type, AddonArgs args)
    {
        var addon = args.Addon.ToAtkUnitBase();

        Callback(addon, true, 0);
        addon->Hide(false, false, 1);
    }

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnLogo);
    }
}
