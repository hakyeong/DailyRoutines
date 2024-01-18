using ClickLib.Bases;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace DailyRoutines.Clicks;

public class ClickRetainerSellDR(nint addon = default)
    : ClickBase<ClickRetainerSellDR, AddonRetainerSell>("RetainerSell", addon)
{
    public bool ComparePrice()
    {
        FireCallback(4);
        return true;
    }

    public bool Confirm()
    {
        FireCallback(0);
        return true;
    }

    public bool Cancel()
    {
        FireCallback(1);
        return true;
    }
}
