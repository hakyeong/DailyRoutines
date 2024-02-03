using System.Linq;
using DailyRoutines.Infos;
using DailyRoutines.Manager;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Party;
using Dalamud.Game.Gui;
using Dalamud.Game.Gui.PartyFinder;
using Dalamud.Game.Gui.Toast;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace DailyRoutines.Managers;

public class Service
{
    public static void Initialize(DalamudPluginInterface pluginInterface)
    {
        Config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Config.Initialize(pluginInterface);
        pluginInterface.Create<Service>();

        InitLanguage();
        ExcelData = new();
    }

    private static void InitLanguage()
    {
        var playerLang = Config.SelectedLanguage;
        if (string.IsNullOrEmpty(playerLang))
        {
            playerLang = ClientState.ClientLanguage.ToString();
            if (LanguageManager.LanguageNames.All(x => x.Language != playerLang))
            {
                playerLang = "English";
            }
            Config.SelectedLanguage = playerLang;
            Config.Save();
        }

        Lang = new LanguageManager(playerLang);
    }

    [PluginService] public static IClientState ClientState { get; private set; } = null!;
    [PluginService] public static Framework Framework { get; private set; } = null!;
    [PluginService] public static KeyState KeyState { get; private set; } = null!;
    [PluginService] public static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] public static Condition Condition { get; private set; } = null!;
    [PluginService] public static IDataManager Data { get; private set; } = null!;
    [PluginService] public static ChatGui Chat { get; private set; } = null!;
    [PluginService] public static ICommandManager Command { get; set; } = null!;
    [PluginService] public static IPluginLog Log { get; private set; } = null!;
    [PluginService] public static PartyFinderGui PartyFinder { get; private set; } = null!;
    [PluginService] public static IGameGui Gui { get; private set; } = null!;
    [PluginService] public static ITargetManager Target { get; private set; } = null!;
    [PluginService] public static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] public static IAddonEventManager AddonEvent { get; private set; } = null!;
    [PluginService] public static ITextureProvider Texture { get; private set; } = null!;
    [PluginService] public static ToastGui Toast { get; private set; } = null!;
    [PluginService] public static PartyList PartyList { get; private set; } = null!;
    public static SigScanner SigScanner { get; private set; } = new();
    public static ExcelGameData ExcelData { get; set; } = null!;
    public static LanguageManager Lang { get; set; } = null!;
    public static Configuration Config { get; set; } = null!;
}
