using ClickLib.Bases;

namespace DailyRoutines.Infos.Clicks;

public class ClickHummer(nint addon = default) : ClickBase<ClickHummer>("Hummer", addon)
{
    public void Play(int grade)
    {
        if (grade is < 0 or > 3) return;
        FireCallback(11, grade, 0);
    }

    public static ClickHummer Using(nint addon) => new(addon);
}
