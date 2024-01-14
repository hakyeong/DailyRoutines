namespace DailyRoutines;

public sealed class Plugin : IDalamudPlugin
{
    public string Name => "Daily Routines";
    public const string CommandName = "/pdr";

    internal DalamudPluginInterface PluginInterface { get; init; }
    public Main? Main { get; private set; }

    public ModuleManager? ModuleManager;
    public WindowSystem WindowSystem = new("SamplePlugin");
    public static Plugin Instance = null!;

    public Plugin(DalamudPluginInterface pluginInterface)
    {
        Instance = this;
        PluginInterface = pluginInterface;

        Service.Initialize(pluginInterface);

        CommandHandler();
        WindowHandler();

        ModuleManager ??= new ModuleManager();
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
        Main.IsOpen = !Main.IsOpen;
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
        ModuleManager.Uninit();
    }
}
