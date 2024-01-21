using DailyRoutines.Infos;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoCutSceneSkipTitle", "AutoCutSceneSkipDescription", ModuleCategories.Retainer)]
public class AutoCutSceneSkip : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithUI => false;

    public void Init()
    {
        Initialized = true;
    }

    public void UI()
    {
        
    }

    public void Uninit()
    {
        Initialized = true;
    }
}
