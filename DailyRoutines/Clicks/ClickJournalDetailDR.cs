using ClickLib.Bases;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace DailyRoutines.Clicks;

// 理符任务专用
public class ClickJournalDetailDR(nint addon = default) : ClickBase<ClickJournalDetailDR>("JournalDetail", addon)
{
    public unsafe bool? Accept(int leveQuestId)
    {
        if (TryGetAddonByName<AddonJournalDetail>("JournalDetail", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(&addon->AtkUnitBase))
        {
            FireCallback(3, leveQuestId);
            return true;
        }

        return false;
    }

    public unsafe bool? Decline(int leveQuestId)
    {
        if (TryGetAddonByName<AddonJournalDetail>("JournalDetail", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(&addon->AtkUnitBase))
        {
            FireCallback(7, leveQuestId);
            return true;
        }

        return false;
    }

    public bool? Exit()
    {
        FireCallback(-2);
        return true;
    }
}
