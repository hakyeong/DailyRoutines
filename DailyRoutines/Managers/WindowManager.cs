using DailyRoutines.Windows;
using Dalamud.Interface.Windowing;

namespace DailyRoutines.Managers;

public class WindowManager
{
    public WindowSystem? WindowSystem { get; private set; }
    public static Main? Main { get; private set; }

    public void Init()
    {
        WindowSystem = new("DailyRoutines");
        Main = new();
        WindowSystem.AddWindow(Main);

        Service.PluginInterface.UiBuilder.Draw += DrawUI;
        Service.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
    }

    private void DrawUI()
    {
        WindowSystem?.Draw();
    }

    public void DrawConfigUI()
    {
        if (Main != null) Main.IsOpen ^= true;
    }

    public void Uninit()
    {
        WindowSystem.RemoveAllWindows();
        Main?.Dispose();
        Main = null;
    }
}
