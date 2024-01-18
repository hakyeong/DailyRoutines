using ClickLib.Bases;

namespace DailyRoutines.Clicks;

public class ClickCharaSelectListMenuDR(nint addon = default) : ClickBase<ClickCharaSelectListMenuDR>("_CharaSelectListMenu", addon)
{
    public bool SelectChara(int index)
    {
        if (index is < 0 or > 10) return false;

        FireCallback(5, index);
        FireCallback(17, 0, index);
        FireCallback(5, index);

        return true;
    }
}
