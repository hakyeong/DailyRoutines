global using static DailyRoutines.Plugin;
global using static ECommons.GenericHelpers;
global using static DailyRoutines.Infos.Widgets;
global using static OmenTools.Helpers.HelpersOm;
global using OmenTools.ImGuiOm;
global using OmenTools.Helpers;
using DailyRoutines.Managers;
using DailyRoutines.Windows;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ECommons;

namespace DailyRoutines;

public sealed class Plugin : IDalamudPlugin
{
    public string Name => "Daily Routines";

    internal DalamudPluginInterface PluginInterface { get; init; }
    public Main? Main { get; private set; }

    public ModuleManager? ModuleManager;
    public WindowSystem WindowSystem = new("DailyRoutines");
    internal static Plugin P = null!;

    public Plugin(DalamudPluginInterface pluginInterface)
    {
        P = this;
        PluginInterface = pluginInterface;
        PluginInterface.UiBuilder.DisableCutsceneUiHide = true;

        ECommonsMain.Init(pluginInterface, this, Module.DalamudReflector);
        Service.Initialize(pluginInterface);

        WindowHandler();

        ModuleManager ??= new ModuleManager();
        ModuleManager.Init();

        WinToast.Init();
    }

    private void WindowHandler()
    {
        Main = new Main(this);
        WindowSystem.AddWindow(Main);

        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
    }

    private void DrawUI()
    {
        WindowSystem.Draw();
    }

    public void DrawConfigUI()
    {
        Main.IsOpen = !Main.IsOpen;
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();
        Main?.Dispose();

        ECommonsMain.Dispose();
        ModuleManager.Uninit();
        Service.Uninit();
    }
}
