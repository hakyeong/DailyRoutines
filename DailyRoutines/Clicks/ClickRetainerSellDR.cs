using ClickLib.Bases;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace DailyRoutines.Clicks;

public class ClickRetainerSellDR(nint addon = default)
    : ClickBase<ClickRetainerSellDR, AddonRetainerSell>("RetainerSell", addon)
{
    public void ComparePrice() => FireCallback(4);

    public void Confirm() => FireCallback(0);

    public void Decline() => FireCallback(1);
}
