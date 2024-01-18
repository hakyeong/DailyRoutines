using ClickLib.Bases;

namespace DailyRoutines.Clicks;

public class ClickTitleMenuDR(nint addon = default) : ClickBase<ClickTitleMenuDR>("_TitleMenu", addon)
{
    public void Start() => FireCallback(1);

    public void Config() => FireCallback(14);

    public void MoviesAndTitle() => FireCallback(15);
}
