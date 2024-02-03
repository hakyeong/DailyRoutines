using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.AddonLifecycle;
using ClickLib.Clicks;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoSellCardsConfirmTitle", "AutoSellCardsConfirmDescription", ModuleCategories.GoldSaucer)]
public class AutoSellCardsConfirm : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithConfigUI => false;
    public void Init()
    {
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "ShopCardDialog", OnAddonSetup);
    }
    public void ConfigUI() { }

    public void OverlayUI() { }

    private unsafe void OnAddonSetup(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)args.Addon;
        if (addon == null) return;

        var handler = new ClickShopCardDialog();
        handler.Sell();
    }

    public void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddonSetup);
    }
}
