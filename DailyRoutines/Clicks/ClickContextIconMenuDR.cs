using ClickLib.Bases;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace DailyRoutines.Clicks;

public class ClickContextIconMenuDR(nint addon = default)
    : ClickBase<ClickContextIconMenuDR, AddonContextIconMenu>("ContextIconMenu", addon)
{
    public bool ClickItem(int iconId, bool isHq)
    {
        var index = int.Parse($"{(isHq ? "10" : "")}{iconId}");
        FireCallback(0, 0, index, 0, 0);
        return true;
    }
}
