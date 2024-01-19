using ClickLib.Bases;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace DailyRoutines.Clicks;

public class ClickContextIconMenuDR(nint addon = default) : ClickBase<ClickContextIconMenuDR, AddonContextIconMenu>("ContextIconMenu", addon)
{
    public void ClickItemByIcon(int iconId, bool isHq)
    {
        var index = int.Parse($"{(isHq ? "10" : "")}{iconId}");
        FireCallback(0, 0, index, 0, 0);
    }

    public void ClickItem() => FireCallback(0, 0, 1021003, 0, 0);
}
