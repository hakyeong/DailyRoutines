namespace DailyRoutines.Managers.Modules;

[ModuleDescription("AutoMiniCactpotTitle", "AutoMiniCactpotDescription", "General")]
public class AutoMiniCactpot : IDailyModule
{
    public bool Initialized { get; set; }

    public void Init()
    {
        Initialized = true;
    }

    public void Uninit()
    {
        Initialized = false;
    }
}
