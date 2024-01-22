using ClickLib.Bases;

namespace DailyRoutines.Clicks;

public class ClickAirShipExplorationResultDR(nint addon = default) : ClickBase<ClickAirShipExplorationResultDR>("AirShipExplorationResult", addon)
{
    // 再次出发
    public void Redeploy() => FireCallback(1);

    // 确认完毕
    public void FinalizeReport() => FireCallback(0);
}
