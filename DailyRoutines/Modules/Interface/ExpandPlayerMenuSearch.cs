using System.Net.Http;
using System.Threading.Tasks;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Utility;
using ImGuiNET;
using Newtonsoft.Json;
namespace DailyRoutines.Modules;

[ModuleDescription("ExpandPlayerMenuSearchTitle", "ExpandPlayerMenuSearchDescription", ModuleCategories.Interface)]
public class ExpandPlayerMenuSearch : DailyModuleBase
{
    public class CharacterSearchInfo
    {
        public string Name { get; set; } = null!;
        public string World { get; set; } = null!;
    }

    private static readonly HttpClient client = new();

    private const string RisingStoneSearchAPI = "https://apiff14risingstones.web.sdo.com/api/common/search?type=6&keywords={0}";
    private const string RisingStonePlayerInfo = "https://ff14risingstones.web.sdo.com/pc/index.html#/me/info?uuid={0}";
    private const string FFLogsSearch = "https://cn.fflogs.com/character/CN/{0}/{1}";
    private const string TiebaSearch = "https://tieba.baidu.com/f/search/res?ie=utf-8&kw=ff14&qw={0}";
    private static CharacterSearchInfo? _TargetChara;

    private static bool RisingStoneEnabled;
    private static bool FFLogsEnabled;
    private static bool TiebaEnabled;

    public override void Init()
    {
        AddConfig(this, "RisingStoneEnabled", true);
        RisingStoneEnabled = GetConfig<bool>(this, "RisingStoneEnabled");

        AddConfig(this, "FFLogsEnabled", true);
        FFLogsEnabled = GetConfig<bool>(this, "FFLogsEnabled");

        AddConfig(this, "TiebaEnabled", true);
        TiebaEnabled = GetConfig<bool>(this, "TiebaEnabled");

        Service.ContextMenu.OnMenuOpened += OnMenuOpen;
        base.Init();
    }

    public override void ConfigUI()
    {
        if (ImGui.Checkbox(Service.Lang.GetText("ExpandPlayerMenuSearch-RisingStoneSearch"), ref RisingStoneEnabled))
            UpdateConfig(this, "RisingStoneEnabled", RisingStoneEnabled);
        if (ImGui.Checkbox(Service.Lang.GetText("ExpandPlayerMenuSearch-FFLogsSearch"), ref FFLogsEnabled))
            UpdateConfig(this, "RisingStoneEnabled", FFLogsEnabled);
        if (ImGui.Checkbox(Service.Lang.GetText("ExpandPlayerMenuSearch-TiebaSearch"), ref TiebaEnabled))
            UpdateConfig(this, "RisingStoneEnabled", TiebaEnabled);
    }

    private static void OnMenuOpen(MenuOpenedArgs args)
    {
        if (!IsValidAddon(args)) return;
        if (args.MenuType != ContextMenuType.Default) return;

        if (RisingStoneEnabled) args.AddMenuItem(RisingStoneItem);
        if (FFLogsEnabled) args.AddMenuItem(FFLogsItem);
        if (TiebaEnabled) args.AddMenuItem(TiebaItem);
    }

    private static readonly MenuItem RisingStoneItem = new()
    {
        IsEnabled = true,
        IsReturn = false,
        PrefixChar = 'D',
        Name = RPrefix(Service.Lang.GetText("ExpandPlayerMenuSearch-RisingStoneSearch")),
        OnClicked = OnClickRisingStone,
        IsSubmenu = false,
        PrefixColor = 34,
    };

    private static readonly MenuItem FFLogsItem = new()
    {
        IsEnabled = true,
        IsReturn = false,
        PrefixChar = 'D',
        Name = RPrefix(Service.Lang.GetText("ExpandPlayerMenuSearch-FFLogsSearch")),
        OnClicked = OnClickFFLogs,
        IsSubmenu = false,
        PrefixColor = 34,
    };

    private static readonly MenuItem TiebaItem = new()
    {
        IsEnabled = true,
        IsReturn = false,
        PrefixChar = 'D',
        Name = RPrefix(Service.Lang.GetText("ExpandPlayerMenuSearch-TiebaSearch")),
        OnClicked = OnClickTieba,
        IsSubmenu = false,
        PrefixColor = 34,
    };

    private static void OnClickRisingStone(MenuItemClickedArgs args)
    {
        Task.Run(async () =>
        {
            if (_TargetChara == null) return;
            var response = await client.GetStringAsync(string.Format(RisingStoneSearchAPI, _TargetChara.Name));
            var result = JsonConvert.DeserializeObject<FileFormat.RSPlayerSearchResult>(response);

            if (result.data.Count > 0)
            {
                var isFound = false;
                foreach (var player in result.data)
                {
                    if (player.character_name != _TargetChara.Name) continue;
                    if (player.group_name != _TargetChara.World) continue;
                    var uuid = player.uuid;
                    Util.OpenLink(string.Format(RisingStonePlayerInfo, uuid));
                    isFound = true;
                }

                if (!isFound) Service.Chat.PrintError(Service.Lang.GetText("ExpandPlayerMenuSearch-RisingStoneInfoNotFound"), "Daily Routines");

                return;
            }

            Service.Chat.PrintError(Service.Lang.GetText("ExpandPlayerMenuSearch-RisingStoneInfoNotFound"), "Daily Routines");
        });
    }

    private static void OnClickFFLogs(MenuItemClickedArgs args)
    {
        if (_TargetChara == null) return;
        Util.OpenLink(string.Format(FFLogsSearch, _TargetChara.World, _TargetChara.Name));
    }

    private static void OnClickTieba(MenuItemClickedArgs args)
    {
        if (_TargetChara == null) return;
        Util.OpenLink(string.Format(TiebaSearch, $"{_TargetChara.Name}@{_TargetChara.World}"));
    }

    private static unsafe bool IsValidAddon(MenuArgs args)
    {
        if (args.Target is MenuTargetInventory) return false;
        var menuTarget = (MenuTargetDefault)args.Target;

        var agent = Service.Gui.FindAgentInterface("ChatLog");
        if (agent != nint.Zero && *(uint*)(agent + 0x948 + 8) == 3) return false;

        switch (args.AddonName)
        {
            default:
                return false;
            case null:
            case "LookingForGroup":
            case "PartyMemberList":
            case "FriendList":
            case "FreeCompany":
            case "SocialList":
            case "ContactList":
            case "ChatLog":
            case "_PartyList":
            case "LinkShell":
            case "CrossWorldLinkshell":
            case "ContentMemberList":
            case "BlackList":
                if (menuTarget.TargetCharacter != null)
                    _TargetChara = menuTarget.TargetCharacter.ToCharacterSearchInfo();
                else if (menuTarget.TargetObject is Character chara)
                    _TargetChara = chara.ToCharacterSearchInfo();
                else if (!string.IsNullOrWhiteSpace(menuTarget.TargetName) && menuTarget.TargetHomeWorld.GameData != null)
                    _TargetChara = new() { Name = menuTarget.TargetName, World = menuTarget.TargetHomeWorld.GameData.Name.RawString };
                return menuTarget.TargetCharacter != null || menuTarget.TargetObject is Character || (!string.IsNullOrWhiteSpace(menuTarget.TargetName) && menuTarget.TargetHomeWorld.GameData != null);
        }
    }

    public override void Uninit()
    {
        Service.ContextMenu.OnMenuOpened -= OnMenuOpen;

        base.Uninit();
    }
}
