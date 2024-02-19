using ClickLib.Bases;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace DailyRoutines.Clicks;

public unsafe class ClickItemSearchResultDR(nint addon = default) : ClickBase<ClickItemSearchResultDR, AddonItemSearchResult>("ItemSearchResult", addon)
{
    public void History()
    {
        FireCallback(0);
    }

    public void Filter()
    {
        FireCallback(1);
    }
}
