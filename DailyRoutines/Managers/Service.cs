using System.Linq;
using DailyRoutines.Infos;
using Dalamud.Game;
using Dalamud.Game.ClientState.Objects;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace DailyRoutines.Managers;

public class Service
{
    internal static void Initialize(DalamudPluginInterface pluginInterface)
    {
        Config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Config.Initialize(pluginInterface);
        pluginInterface.Create<Service>();

        InitLanguage();
        AddonManager.Init();
        PresetData = new();
        Notice = new();
        Waymarks = new();
        Font = new();
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

    internal static void Uninit()
    {
        AddonManager.Uninit();
        Waymarks.Uninit();
        Config.Uninit();
        Notice.Dispose();
    }

    [PluginService] public static IClientState ClientState { get; private set; } = null!;
    [PluginService] public static IFramework Framework { get; private set; } = null!;
    [PluginService] public static IKeyState KeyState { get; private set; } = null!;
    [PluginService] public static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] public static ICondition Condition { get; private set; } = null!;
    [PluginService] public static IDataManager Data { get; private set; } = null!;
    [PluginService] public static IChatGui Chat { get; private set; } = null!;
    [PluginService] public static ICommandManager Command { get; set; } = null!;
    [PluginService] public static IPluginLog Log { get; private set; } = null!;
    [PluginService] public static IPartyFinderGui PartyFinder { get; private set; } = null!;
    [PluginService] public static IGameGui Gui { get; private set; } = null!;
    [PluginService] public static ITargetManager Target { get; private set; } = null!;
    [PluginService] public static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] public static IAddonEventManager AddonEvent { get; private set; } = null!;
    [PluginService] public static ITextureProvider Texture { get; private set; } = null!;
    [PluginService] public static IToastGui Toast { get; private set; } = null!;
    [PluginService] public static IPartyList PartyList { get; private set; } = null!;
    [PluginService] public static IDutyState DutyState { get; private set; } = null!;
    [PluginService] public static IGameInteropProvider Hook { get; private set; } = null!;
    public static SigScanner SigScanner { get; private set; } = new();
    public static FontManager Font { get; private set; } = null!;
    public static PresetExcelData PresetData { get; set; } = null!;
    public static FieldMarkerManager Waymarks { get; set; } = null!;
    public static NotificationManager Notice { get; private set; } = null!;
    public static LanguageManager Lang { get; set; } = null!;
    public static Configuration Config { get; set; } = null!;
}
