using ClickLib.Bases;

namespace DailyRoutines.Clicks;

// 潜水艇专用
public class ClickCompanyCraftSupplyDR(nint addon = default) : ClickBase<ClickCompanyCraftSupplyDR>("CompanyCraftSupply", addon)
{
    public void Component(int index) => FireCallback(3, 0, index, 0, 0);

    public void Close() => FireCallback(5);
}
