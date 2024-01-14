namespace DailyRoutines.Infos;

public interface IDailyModule
{
    void Init();

    void Uninit();

    bool Initialized { get; set; }
}
