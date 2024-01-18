using ClickLib.Bases;

namespace DailyRoutines.Clicks;

public class ClickRequestDR(nint addon = default) : ClickBase<ClickRequestDR>("Request", addon)
{
    public void IconBox() => FireCallback(2, 0, 44, 0);
}
