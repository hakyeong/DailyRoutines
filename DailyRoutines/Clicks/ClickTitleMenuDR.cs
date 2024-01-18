using ClickLib.Bases;

namespace DailyRoutines.Clicks;

public class ClickTitleMenuDR(nint addon = default) : ClickBase<ClickTitleMenuDR>("_TitleMenu", addon)
{
    public bool Start()
    {
        FireCallback(1);
        return true;
    }

    public bool Config()
    {
        FireCallback(14);
        return true;
    }

    public bool MoviesAndTitle()
    {
        FireCallback(15);
        return true;
    }
}
