using ClickLib.Bases;

namespace DailyRoutines.Clicks;

public class ClickRetainerSellListDR(nint addon = default) : ClickBase<ClickRetainerSellListDR>("RetainerSellList", addon)
{
    public bool ClickItem(int index)
    {
        if (index is > 19 or < -1) return false;

        FireCallback(0, index, 1);

        return true;
    }
}

