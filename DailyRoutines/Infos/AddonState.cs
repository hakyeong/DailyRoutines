using DailyRoutines.Managers;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace DailyRoutines.Infos;

public static unsafe class AddonState
{
    public static AtkUnitBase* Inventory                => GetAddonByName("Inventory");
    public static AtkUnitBase* InventoryLarge           => GetAddonByName("InventoryLarge");
    public static AtkUnitBase* InventoryExpansion       => GetAddonByName("InventoryExpansion");
    public static AtkUnitBase* SelectString             => GetAddonByName("SelectString");
    public static AtkUnitBase* InputNumeric             => GetAddonByName("InputNumeric");
    public static AtkUnitBase* ChatLog                  => GetAddonByName("ChatLog");
    public static AtkUnitBase* CharaCard                => GetAddonByName("CharaCard");
    public static AtkUnitBase* SelectYesno              => GetAddonByName("SelectYesno");
    public static AtkUnitBase* TitleMenu                => GetAddonByName("_TitleMenu");
    public static AtkUnitBase* MateriaAttach            => GetAddonByName("MateriaAttach");
    public static AtkUnitBase* MateriaAttachDialog      => GetAddonByName("MateriaAttachDialog");
    public static AtkUnitBase* MateriaRetrieveDialog    => GetAddonByName("MateriaRetrieveDialog");
    public static AtkUnitBase* PartyList                => GetAddonByName("_PartyList");
    public static AtkUnitBase* NowLoading               => GetAddonByName("NowLoading");
    public static AtkUnitBase* FadeMiddle               => GetAddonByName("FadeMiddle");
    public static AtkUnitBase* FadeBack                 => GetAddonByName("FadeBack");
    public static AtkUnitBase* CharacterInspect         => GetAddonByName("CharacterInspect");
    public static AtkUnitBase* CharaSelectListMenu      => GetAddonByName("_CharaSelectListMenu");
    public static AtkUnitBase* CollectablesShop         => GetAddonByName("CollectablesShop");
    public static AtkUnitBase* GrandCompanySupplyReward => GetAddonByName("GrandCompanySupplyReward");
    public static AtkUnitBase* GrandCompanySupplyList   => GetAddonByName("GrandCompanySupplyList");
    public static AtkUnitBase* CharaSelectWorldServer   => GetAddonByName("_CharaSelectWorldServer");

    public static AtkUnitBase* ToAtkUnitBase(this nint ptr) => (AtkUnitBase*)ptr;

    public static AtkUnitBase* GetAddonByName(string name) => (AtkUnitBase*)Service.Gui.GetAddonByName(name);
}
