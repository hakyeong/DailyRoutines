using ClickLib.Bases;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace DailyRoutines.Infos.Clicks;

public class ClickCollectableShop(nint addon = default) : ClickBase<ClickCollectableShop, AddonSelectYesno>("CollectableShop", addon)
{
    public unsafe void Exchange()
    {
        var atkUnitBase = AddonAddress.ToAtkUnitBase();
        if (atkUnitBase == null) return;

        var buttonNode = atkUnitBase->GetButtonNodeById(51);
        ClickAddonButton(buttonNode, 12);
    }

    public static ClickCollectableShop Using(nint addon) => new(addon);
}
