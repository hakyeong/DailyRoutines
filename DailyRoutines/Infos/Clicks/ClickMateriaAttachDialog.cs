using ClickLib.Bases;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace DailyRoutines.Infos.Clicks;

public class ClickMateriaAttachDialog(nint addon = default)
    : ClickBase<ClickMateriaAttachDialog, AddonSelectYesno>("MateriaAttachDialog", addon)
{
    public unsafe void Confirm()
    {
        var atkUnitBase = AddonAddress.ToAtkUnitBase();
        if (atkUnitBase == null) return;

        var buttonNode = atkUnitBase->GetButtonNodeById(35);
        ClickAddonButton(buttonNode, 0);
    }

    public unsafe void Cancel()
    {
        var atkUnitBase = AddonAddress.ToAtkUnitBase();
        if (atkUnitBase == null) return;

        var buttonNode = atkUnitBase->GetButtonNodeById(36);
        ClickAddonButton(buttonNode, 1);
    }

    public static ClickMateriaAttachDialog Using(nint addon) => new(addon);
}
