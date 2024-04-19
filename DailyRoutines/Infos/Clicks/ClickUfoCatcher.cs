using ClickLib.Bases;

namespace DailyRoutines.Infos.Clicks;

public class ClickUfoCatcher(nint addon = default) : ClickBase<ClickUfoCatcher>("UfoCatcher", addon)
{
    public void Miss() => Play(0);

    public void SmallBall() => Play(1);

    public void BigBall() => Play(3);

    public void Play(int index) => FireCallback(11, index, 0);

    public static ClickUfoCatcher Using(nint addon) => new(addon);
}
