namespace DailyRoutines.Infos;

public interface IDailyModule
{
    bool Initialized { get; set; }

    bool WithUI { get; }

    void Init();

    void UI();

    void Uninit();
}
