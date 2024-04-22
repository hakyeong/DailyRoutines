using System.Linq;
using DailyRoutines.Infos;
using DailyRoutines.Windows;
using Dalamud.Interface.Windowing;

namespace DailyRoutines.Managers;

public class WindowManager : IDailyManager
{
    public WindowSystem? WindowSystem { get; private set; }
    public static Main? Main { get; private set; }

    private void Init()
    {
        WindowSystem = new("DailyRoutines");
        Main = new();
        AddWindows(Main);

        Service.PluginInterface.UiBuilder.Draw += DrawUI;
        Service.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
    }

    public bool AddWindows(Window? window)
    {
        if (window == null) return false;

        var addedWindows = WindowSystem.Windows;
        if (addedWindows.Contains(window) || addedWindows.Any(x => x.WindowName == window.WindowName))
            return false;

        WindowSystem.AddWindow(window);
        return true;
    }

    public bool RemoveWindows(Window? window)
    {
        if (window == null) return false;

        var addedWindows = WindowSystem.Windows;
        if (!addedWindows.Contains(window))
            return false;

        WindowSystem.RemoveWindow(window);
        return true;
    }

    private void DrawUI()
    {
        WindowSystem?.Draw();
    }

    public void DrawConfigUI()
    {
        if (Main != null) Main.IsOpen ^= true;
    }

    private void Uninit()
    {
        WindowSystem.RemoveAllWindows();
        Main?.Dispose();
        Main = null;
    }
}
