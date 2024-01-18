using ClickLib.Bases;

namespace DailyRoutines.Clicks;

public class ClickCharaSelectWorldServerDR(nint addon = default) : ClickBase<ClickCharaSelectWorldServerDR>("_CharaSelectWorldServer", addon)
{
    public void SelectWorld(int index) => FireCallback(9, 0, index);
}
