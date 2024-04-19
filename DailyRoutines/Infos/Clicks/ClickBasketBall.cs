using ClickLib.Bases;

namespace DailyRoutines.Infos.Clicks;

public class ClickBasketBall(nint addon = default) : ClickBase<ClickBasketBall>("BasketBall", addon)
{
    public void Play(bool isHit) => FireCallback(11, isHit ? 1 : 0, 0);

    public static ClickBasketBall Using(nint addon) => new(addon);
}
