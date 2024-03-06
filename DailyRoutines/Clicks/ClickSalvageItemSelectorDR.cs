using ClickLib.Bases;

namespace DailyRoutines.Clicks;

public class ClickSalvageItemSelectorDR(nint addon = default) : ClickBase<ClickSalvageItemSelectorDR>("SalvageItemSelector", addon)
{
    public void Item(int index) => FireCallback(12, index);
}
