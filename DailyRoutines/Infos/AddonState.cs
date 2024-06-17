using DailyRoutines.Managers;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace DailyRoutines.Infos;

public static unsafe class AddonState
{
    public static AtkUnitBase* Inventory           => (AtkUnitBase*)Service.Gui.GetAddonByName("Inventory");
    public static AtkUnitBase* InventoryLarge      => (AtkUnitBase*)Service.Gui.GetAddonByName("InventoryLarge");
    public static AtkUnitBase* InventoryExpansion  => (AtkUnitBase*)Service.Gui.GetAddonByName("InventoryExpansion");
    public static AtkUnitBase* SelectString        => (AtkUnitBase*)Service.Gui.GetAddonByName("SelectString");
    public static AtkUnitBase* InputNumeric        => (AtkUnitBase*)Service.Gui.GetAddonByName("InputNumeric");
    public static AtkUnitBase* ChatLog             => (AtkUnitBase*)Service.Gui.GetAddonByName("ChatLog");
    public static AtkUnitBase* SelectYesno         => (AtkUnitBase*)Service.Gui.GetAddonByName("SelectYesno");
    public static AtkUnitBase* TitleMenu           => (AtkUnitBase*)Service.Gui.GetAddonByName("_TitleMenu");
    public static AtkUnitBase* CharaSelectListMenu => (AtkUnitBase*)Service.Gui.GetAddonByName("_CharaSelectListMenu");
    public static AtkUnitBase* CollectablesShop    => (AtkUnitBase*)Service.Gui.GetAddonByName("CollectablesShop");

    public static AtkUnitBase* GrandCompanySupplyReward =>
        (AtkUnitBase*)Service.Gui.GetAddonByName("GrandCompanySupplyReward");
    public static AtkUnitBase* GrandCompanySupplyList =>
        (AtkUnitBase*)Service.Gui.GetAddonByName("GrandCompanySupplyList");
    public static AtkUnitBase* CharaSelectWorldServer =>
        (AtkUnitBase*)Service.Gui.GetAddonByName("_CharaSelectWorldServer");

    public static AtkUnitBase* ToAtkUnitBase(this nint ptr) => (AtkUnitBase*)ptr;
}
