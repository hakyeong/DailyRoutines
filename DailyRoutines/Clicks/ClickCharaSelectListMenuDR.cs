using ClickLib.Bases;

namespace DailyRoutines.Clicks;

public class ClickCharaSelectListMenuDR(nint addon = default) : ClickBase<ClickCharaSelectListMenuDR>("_CharaSelectListMenu", addon)
{
    public void SelectChara(int index)
    {
        FireCallback(6, index);
        FireCallback(18, 0, index);
        FireCallback(6, index);
    }
}
