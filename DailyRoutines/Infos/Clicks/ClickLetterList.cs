using ClickLib.Bases;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace DailyRoutines.Infos.Clicks;

public unsafe class ClickLetterList(nint ptr = default) : ClickBase<ClickLetterList, AddonSelectYesno>("LetterList", ptr)
{
    public void FriendLetter()
    {
        var addon = AddonAddress.ToAtkUnitBase();
        if (addon == null) return;

        var buttonNode = (AtkComponentRadioButton*)addon->GetButtonNodeById(6);
        if (buttonNode == null) return;

        SetCategoryEnabled(6);
        ClickAddonRadioButton(buttonNode, 3);
    }

    public void PurchaseLetter()
    {
        var addon = AddonAddress.ToAtkUnitBase();
        if (addon == null) return;

        var buttonNode = (AtkComponentRadioButton*)addon->GetButtonNodeById(7);
        if (buttonNode == null) return;

        SetCategoryEnabled(7);
        ClickAddonRadioButton(buttonNode, 4);
    }

    public void GMLetter()
    {
        var addon = AddonAddress.ToAtkUnitBase();
        if (addon == null) return;

        var buttonNode = (AtkComponentRadioButton*)addon->GetButtonNodeById(8);
        if (buttonNode == null) return;

        SetCategoryEnabled(8);
        ClickAddonRadioButton(buttonNode, 5);
    }

    private void SetCategoryEnabled(uint nodeID)
    {
        var addon = AddonAddress.ToAtkUnitBase();
        if (addon == null) return;

        for (var i = 6U; i < 9U; i++)
        {
            var buttonNode = addon->GetButtonNodeById(i);
            if (buttonNode == null) continue;

            SetComponentButtonChecked(buttonNode, i == nodeID);
        }
    }

    public static ClickLetterList Using(nint addon) => new(addon);
}
