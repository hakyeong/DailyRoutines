using ClickLib.Bases;

namespace DailyRoutines.Clicks;


public class ClickChatLogDR(nint addon = default) : ClickBase<ClickChatLogDR>("ChatLog", addon)
{
    public void LogWindow() => FireCallback(2);

    public void NoviceNetwork() => FireCallback(3);
}
