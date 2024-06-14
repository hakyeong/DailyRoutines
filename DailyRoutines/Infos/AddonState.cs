using DailyRoutines.Managers;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace DailyRoutines.Infos;

public static unsafe class AddonState
{
    public static AtkUnitBase* Inventory          => (AtkUnitBase*)Service.Gui.GetAddonByName("Inventory");
    public static AtkUnitBase* InventoryLarge     => (AtkUnitBase*)Service.Gui.GetAddonByName("InventoryLarge");
    public static AtkUnitBase* InventoryExpansion => (AtkUnitBase*)Service.Gui.GetAddonByName("InventoryExpansion");
    public static AtkUnitBase* SelectString       => (AtkUnitBase*)Service.Gui.GetAddonByName("SelectString");
    public static AtkUnitBase* InputNumeric       => (AtkUnitBase*)Service.Gui.GetAddonByName("InputNumeric");
    public static AtkUnitBase* ChatLog            => (AtkUnitBase*)Service.Gui.GetAddonByName("ChatLog");

    public static AtkUnitBase* ToAtkUnitBase(this nint ptr) => (AtkUnitBase*)ptr;
}
