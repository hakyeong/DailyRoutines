using ClickLib.Bases;

namespace DailyRoutines.Clicks;

public class ClickHummerDR(nint addon = default) : ClickBase<ClickHummerDR>("Hummer", addon)
{
    public void Play(int grade)
    {
        if (grade is < 0 or > 3) return;
        FireCallback(11, grade, 0);
    }
}
