using ClickLib.Bases;

namespace DailyRoutines.Clicks;

public class ClickAirShipExplorationDetailDR(nint addon = default) : ClickBase<ClickAirShipExplorationDetailDR>("AirShipExplorationDetail", addon)
{
    public void Commence() => FireCallback(0);

    public void Cancel() => FireCallback(-1);
}
