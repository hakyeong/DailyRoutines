using ClickLib.Bases;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace DailyRoutines.Infos.Clicks;

public unsafe class ClickGrandCompanySupplyList(nint addon = default) : 
    ClickBase<ClickGrandCompanySupplyList, AddonGrandCompanySupplyReward>("GrandCompanySupplyList", addon)
{
    public void ItemEntry(int index) => FireCallback(1, index, 0);

    // 筹备军需品
    public void Supply()
    {
        var atkUnitBase = AddonAddress.ToAtkUnitBase();
        if (atkUnitBase == null) return;

        var buttonNode = atkUnitBase->GetNodeById(11)->GetAsAtkComponentRadioButton();
        ClickAddonRadioButton(buttonNode, 2);
    }

    // 筹备补给品
    public void Provisioning()
    {
        var atkUnitBase = AddonAddress.ToAtkUnitBase();
        if (atkUnitBase == null) return;

        var buttonNode = atkUnitBase->GetNodeById(12)->GetAsAtkComponentRadioButton();
        ClickAddonRadioButton(buttonNode, 3);
    }

    // 筹备稀有品
    public void ExpertDelivery()
    {
        var atkUnitBase = AddonAddress.ToAtkUnitBase();
        if (atkUnitBase == null) return;

        var buttonNode = atkUnitBase->GetNodeById(13)->GetAsAtkComponentRadioButton();
        ClickAddonRadioButton(buttonNode, 4);
    }

    public static ClickGrandCompanySupplyList Using(nint addon) => new(addon);
}
