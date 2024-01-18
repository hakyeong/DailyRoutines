using ClickLib.Bases;

namespace DailyRoutines.Clicks;

public class ClickJournalResultDR(nint addon = default) : ClickBase<ClickJournalResultDR>("JournalResult", addon)
{
    public bool? Exit()
    {
        FireCallback(-2);
        return true;
    }
}
