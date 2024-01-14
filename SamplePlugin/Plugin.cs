namespace SamplePlugin;

public sealed class Plugin : IDalamudPlugin
{
    public string Name => "Omen Sample Plugin";
    public const string CommandName = "/omspp";

    private DalamudPluginInterface PluginInterface { get; init; }
    public ConfigWindow? ConfigWindow { get; private set; }

    public WindowSystem WindowSystem = new("SamplePlugin");
    public static Plugin Instance = null!;

    public Plugin(DalamudPluginInterface pluginInterface)
    {
        Instance = this;
        PluginInterface = pluginInterface;

        Service.Initialize(pluginInterface);

        CommandHandler();
        WindowHandler();
    }

    internal void CommandHandler()
    {
        var helpMessage = "A useful message to display in /xlhelp";

        Service.Command.RemoveHandler(CommandName);
        Service.Command.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = helpMessage
        });
    }

    private void WindowHandler()
    {
        ConfigWindow = new ConfigWindow(this);
        WindowSystem.AddWindow(ConfigWindow);

        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
    }

    private void OnCommand(string command, string args)
    {
        ConfigWindow.IsOpen = !ConfigWindow.IsOpen;
    }

    private void DrawUI()
    {
        WindowSystem.Draw();
    }

    public void DrawConfigUI()
    {
        ConfigWindow.IsOpen = true;
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();

        Service.Config.Uninitialize();
    }
}
