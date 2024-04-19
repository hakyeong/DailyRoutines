using ClickLib.Bases;

namespace DailyRoutines.Infos.Clicks;

public class ClickPunchingMachine(nint addon = default) : ClickBase<ClickPunchingMachine>("PunchingMachine", addon)
{
    /// <summary>
    /// 有效范围: 0 - 2000
    /// </summary>
    /// <param name="result"></param>
    public void Play(int result) => FireCallback(11, 3, result);

    public static ClickPunchingMachine Using(nint addon) => new(addon);
}
