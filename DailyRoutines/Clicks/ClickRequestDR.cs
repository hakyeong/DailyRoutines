using ClickLib.Bases;

namespace DailyRoutines.Clicks;

public class ClickRequestDR(nint addon = default) : ClickBase<ClickRequestDR>("Request", addon)
{
    public bool Click()
    {
        FireCallback(2, 0, 44, 0);
        return true;
    }
}
