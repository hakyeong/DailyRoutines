using ClickLib.Bases;

namespace DailyRoutines.Clicks;

public class ClickCharaSelectListMenuDR(nint addon = default) : ClickBase<ClickCharaSelectListMenuDR>("_CharaSelectListMenu", addon)
{
    public void SelectChara(int index)
    {
        FireCallback(5, index);
        FireCallback(17, 0, index);
        FireCallback(5, index);
    }
}
