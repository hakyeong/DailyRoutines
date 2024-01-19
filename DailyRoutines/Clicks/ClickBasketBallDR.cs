using ClickLib.Bases;

namespace DailyRoutines.Clicks;

public class ClickBasketBallDR(nint addon = default) : ClickBase<ClickBasketBallDR>("BasketBall", addon)
{
    public void Play(bool isHit) => FireCallback(11, isHit ? 1 : 0, 0);
}
