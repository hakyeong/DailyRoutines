using ClickLib.Bases;

namespace DailyRoutines.Clicks;

public class ClickRetainerSellListDR(nint addon = default) : ClickBase<ClickRetainerSellListDR>("RetainerSellList", addon)
{
    public void ItemEntry(int index) => FireCallback(0, index, 1);
}

