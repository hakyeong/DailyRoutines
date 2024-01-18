using ClickLib.Bases;

namespace DailyRoutines.Clicks;

public class ClickRetainerSellListContextMenuDR(nint addon = default)
    : ClickBase<ClickRetainerSellListContextMenuDR>("ContextMenu", addon)
{
    public bool AdjustPrice()
    {
        Click(0);
        return true;
    }

    public bool ReturnToRetainer()
    {
        Click(1);
        return true;
    }

    public bool ReturnToInventory()
    {
        Click(2);
        return true;
    }

    public bool Exit()
    {
        Click(-1);
        return true;
    }

    public bool Click(int index)
    {
        FireCallback(0, index, 0, 0, 0);
        return true;
    }
}
