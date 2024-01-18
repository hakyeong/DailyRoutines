using ClickLib.Bases;

namespace DailyRoutines.Clicks;

// 理符任务专用
public class ClickJournalDetailDR(nint addon = default) : ClickBase<ClickJournalDetailDR>("JournalDetail", addon)
{
    public void Accept(int leveQuestId) => FireCallback(3, leveQuestId);

    public void Decline(int leveQuestId) => FireCallback(7, leveQuestId);

    public void Exit() => FireCallback(-2);
}
