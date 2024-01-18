using ClickLib.Bases;

namespace DailyRoutines.Clicks;

public class ClickPunchingMachineDR(nint addon = default) : ClickBase<ClickPunchingMachineDR>("PunchingMachine", addon)
{
    public void Button(int result) => FireCallback(11, 3, result);
}
