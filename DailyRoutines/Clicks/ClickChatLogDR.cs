using ClickLib.Bases;

namespace DailyRoutines.Clicks;


public class ClickChatLogDR(nint addon = default) : ClickBase<ClickChatLogDR>("ChatLog", addon)
{
    public bool NoviceNetworkButton()
    {
        FireCallback(3);
        return true;
    }
}
