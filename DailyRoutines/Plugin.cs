global using static DailyRoutines.Plugin;
global using static ECommons.GenericHelpers;
global using static DailyRoutines.Infos.Widgets;
global using static OmenTools.Helpers.HelpersOm;
global using OmenTools.ImGuiOm;
global using OmenTools.Helpers;
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
        PluginInterface.UiBuilder.DisableCutsceneUiHide = true;

        ECommonsMain.Init(pluginInterface, this, Module.DalamudReflector);
        Service.Initialize(pluginInterface);

        CommandHandler();
        WindowHandler();

        ModuleManager ??= new ModuleManager();
        ModuleManager.Init();

        Service.Notice.Init();
    }

    internal void CommandHandler()
    {
        var helpMessage = Service.Lang.GetText("CommandHelp");

        Service.Command.RemoveHandler(CommandName);
        Service.Command.AddHandler(CommandName, new CommandInfo(OnCommand) { HelpMessage = helpMessage });
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
            if (string.IsNullOrEmpty(args))
                Main.SearchString = string.Empty;

            Main.IsOpen ^= true;
        }
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
