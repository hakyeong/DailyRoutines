global using static DailyRoutines.Plugin;
global using static ECommons.GenericHelpers;
global using OmenTools.ImGuiOm;
global using OmenTools.Helpers;
global using OmenTools.Widgets;
using DailyRoutines.Manager;
using DailyRoutines.Managers;
using DailyRoutines.Windows;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ECommons;

namespace DailyRoutines;

public sealed class Plugin : IDalamudPlugin
{
    public string Name => "Daily Routines";
    public const string CommandName = "/pdr";

    internal DalamudPluginInterface PluginInterface { get; init; }
    public Main? Main { get; private set; }

    public ModuleManager? ModuleManager;
    public WindowSystem WindowSystem = new("DailyRoutines");
    internal static Plugin P = null!;

    public Plugin(DalamudPluginInterface pluginInterface)
    {
        P = this;
        PluginInterface = pluginInterface;

        Service.Initialize(pluginInterface);
        ECommonsMain.Init(pluginInterface, this, Module.DalamudReflector);

        CommandHandler();
        WindowHandler();

        ModuleManager ??= new ModuleManager();
        ModuleManager.Init();
    }

    internal void CommandHandler()
    {
        var helpMessage = Service.Lang.GetText("CommandHelp");

        Service.Command.RemoveHandler(CommandName);
        Service.Command.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = helpMessage
        });
    }

    private void WindowHandler()
    {
        Main = new Main(this);
        WindowSystem.AddWindow(Main);

        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
    }

    private void OnCommand(string command, string args)
    {
        if (!string.IsNullOrEmpty(args) && args != Main.SearchString)
        {
            Main.SearchString = args;
            Main.IsOpen = true;
        }
        else
        {
            Main.IsOpen = !Main.IsOpen;
        }
    }

    private void DrawUI()
    {
        WindowSystem.Draw();
    }

    public void DrawConfigUI()
    {
        Main.IsOpen = true;
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();
        Main.Dispose();

        Service.Config.Uninitialize();
        ECommonsMain.Dispose();
        ModuleManager.Uninit();
        NotificationManager.Dispose();
    }
}
