namespace DailyRoutines.Infos;

public interface IDailyModule
{
    bool Initialized { get; set; }

    bool WithConfigUI { get; }

    void Init();

    void ConfigUI();

    void OverlayUI();

    void Uninit();
}
