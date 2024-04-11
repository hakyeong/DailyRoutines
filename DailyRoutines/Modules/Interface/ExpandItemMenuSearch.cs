using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;

namespace DailyRoutines.Modules;

[ModuleDescription("ExpandItemMenuSearchTitle", "ExpandItemMenuSearchDescription", ModuleCategories.Interface)]
public class ExpandItemMenuSearch : DailyModuleBase
{
    public override string? Author { get; set; } = "HSS";

    private static Item? _LastItem;
    private static bool SearchCollector;
    private static bool SearchWiki;
    private const int ChatLogContextItemId = 0x948;

    private const string CollectorUrl = "https://www.ffxivsc.cn/#/search?text={0}&type=armor";
    private const string WikiUrl = "https://ff14.huijiwiki.com/index.php?search={0}&profile=default&fulltext=1";

    public override void Init()
    {
        AddConfig(this, "SearchCollector", true);
        AddConfig(this, "SearchWiki", true);
        SearchCollector = GetConfig<bool>(this, "SearchCollector");
        SearchWiki = GetConfig<bool>(this, "SearchWiki");
        Service.ContextMenu.OnMenuOpened += OnMenuOpened;
    }

    public override void ConfigUI()
    {
        if (ImGui.Checkbox(Service.Lang.GetText("ExpandItemMenuSearch-CollectorSearch"), ref SearchCollector))
            UpdateConfig(this, "SearchCollector", SearchCollector);
        if (ImGui.Checkbox(Service.Lang.GetText("ExpandItemMenuSearch-WikiSearch"), ref SearchWiki))
            UpdateConfig(this, "SearchWiki", SearchWiki);
    }

    private static unsafe void OnMenuOpened(MenuOpenedArgs args)
    {
        if (args.MenuType is ContextMenuType.Inventory)
        {
            var arg = (MenuTargetInventory)args.Target;
            if (arg.TargetItem.HasValue)
            {
                if (TryGetItemByID(arg.TargetItem.Value.ItemId) && SearchCollector) args.AddMenuItem(CollectorItem);

                if (SearchWiki)
                {
                    _LastItem = Service.Data.GetExcelSheet<Item>().GetRow(arg.TargetItem.Value.ItemId);
                    args.AddMenuItem(WikiItem);
                }
            }
        }
        else
        {
            switch (args.AddonName)
            {
                case "ItemSearch" when args.AgentPtr != nint.Zero:
                {
                    var itemId = (uint)AgentContext.Instance()->UpdateCheckerParam;
                    if (TryGetItemByID(itemId) && SearchCollector) args.AddMenuItem(CollectorItem);

                    if (SearchWiki)
                    {
                        _LastItem = Service.Data.GetExcelSheet<Item>().GetRow(itemId);
                        args.AddMenuItem(WikiItem);
                    }

                    break;
                }
                case "ChatLog":
                {
                    var agent = Service.Gui.FindAgentInterface("ChatLog");
                    if (agent == nint.Zero || !IsValidChatLogContext(agent))
                        return;
                    var itemId = *(uint*)(agent + ChatLogContextItemId);
                    if (TryGetItemByID(itemId) && SearchCollector) args.AddMenuItem(CollectorItem);

                    if (SearchWiki)
                    {
                        _LastItem = Service.Data.GetExcelSheet<Item>().GetRow(itemId);
                        args.AddMenuItem(WikiItem);
                    }

                    break;
                }
            }
        }
    }

    private static readonly MenuItem CollectorItem = new()
    {
        IsEnabled = true,
        IsReturn = false,
        PrefixChar = 'D',
        Name = RPrefix(Service.Lang.GetText("ExpandItemMenuSearch-CollectorSearch")),
        OnClicked = OnCollector,
        IsSubmenu = false,
        PrefixColor = 34
    };

    private static readonly MenuItem WikiItem = new()
    {
        IsEnabled = true,
        IsReturn = false,
        PrefixChar = 'D',
        Name = RPrefix(Service.Lang.GetText("ExpandItemMenuSearch-WikiSearch")),
        OnClicked = OnWiki,
        IsSubmenu = false,
        PrefixColor = 34
    };

    private static void OnCollector(MenuItemClickedArgs _)
    {
        if (_LastItem != null) Util.OpenLink(string.Format(CollectorUrl, _LastItem.Name));
    }

    private static void OnWiki(MenuItemClickedArgs _)
    {
        if (_LastItem != null) Util.OpenLink(string.Format(WikiUrl, _LastItem.Name));
    }

    private static bool TryGetItemByID(uint id)
    {
        return Service.PresetData.EquipmentItems.TryGetValue(id, out _LastItem);
    }

    private static unsafe bool IsValidChatLogContext(nint agent)
    {
        return *(uint*)(agent + ChatLogContextItemId + 8) == 3;
    }

    public override void Uninit()
    {
        Service.ContextMenu.OnMenuOpened -= OnMenuOpened;
        base.Uninit();
    }
}
