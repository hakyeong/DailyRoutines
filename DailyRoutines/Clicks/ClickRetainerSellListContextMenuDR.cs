using ClickLib.Bases;

namespace DailyRoutines.Clicks;

// 雇员出售列表专用
public class ClickRetainerSellListContextMenuDR(nint addon = default) : ClickBase<ClickRetainerSellListContextMenuDR>("ContextMenu", addon)
{
    public void AdjustPrice() => Click(0);

    public void ReturnToRetainer() => Click(1);

    public void ReturnToInventory() => Click(2);

    public void Exit() => Click(-1);

    public void Click(int index) => FireCallback(0, index, 0, 0, 0);
}
