using ClickLib.Bases;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace DailyRoutines.Clicks;

public class ClickUfoCatcherDR(nint addon = default) : ClickBase<ClickUfoCatcherDR>("UfoCatcher", addon)
{
    public void Miss() => Play(0);

    public void SmallBall() => Play(1);

    public void BigBall() => Play(3);

    public void Play(int index) => FireCallback(11, index, 0);
}
