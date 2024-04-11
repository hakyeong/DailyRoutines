using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

    private static Item? _lastItem;
    private static bool SearchCollector;
    private static bool SearchWiki;
    private const int ChatLogContextItemId = 0x948;
    private const string CollectorUrl = "https://www.ffxivsc.cn/#/search?text={0}&type=armor";
    private const string WikiUrl = "https://ff14.huijiwiki.com/index.php?search={0}&profile=default&fulltext=1";

    private static readonly MenuItem _inventoryCollectorItem = new MenuItem
    {
        IsEnabled = true,
        IsReturn = false,
        PrefixChar = 'D',
        Name = Service.Lang.GetText("ExpandItemMenuSearch-contextMenuCollectorText"),
        OnClicked = OnClick,
        IsSubmenu = false,
        PrefixColor = 541,
    };

    private static readonly MenuItem _inventoryWikiItem = new MenuItem
    {
        IsEnabled = true,
        IsReturn = false,
        PrefixChar = 'D',
        Name = Service.Lang.GetText("ExpandItemMenuSearch-contextWikiMenuText"),
        OnClicked = OnWikiClick,
        IsSubmenu = false,
        PrefixColor = 541,
    };

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
        if (ImGui.Checkbox(Service.Lang.GetText("ExpandItemMenuSearch-SearchCollectorConfig"),
                           ref SearchCollector))
            UpdateConfig(this, "SearchCollector", SearchCollector);
        if (ImGui.Checkbox(Service.Lang.GetText("ExpandItemMenuSearch-SearchWikiConfig"),
                           ref SearchWiki))
            UpdateConfig(this, "SearchWiki", SearchWiki);
    }

    private static unsafe void OnMenuOpened(MenuOpenedArgs args)
    {
        if (args.MenuType is ContextMenuType.Inventory)
        {
            var arg = (MenuTargetInventory)args.Target;
            if (arg.TargetItem.HasValue)
            {
                if (HandleItem(arg.TargetItem.Value.ItemId) && SearchCollector)
                {
                    args.AddMenuItem(_inventoryCollectorItem);
                }

                if (SearchWiki)
                {
                    _lastItem = Service.Data.GetExcelSheet<Item>().GetRow(arg.TargetItem.Value.ItemId);
                    args.AddMenuItem(_inventoryWikiItem);
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
                    if (HandleItem(itemId) && SearchCollector)
                    {
                        args.AddMenuItem(_inventoryCollectorItem);
                    }

                    if (SearchWiki)
                    {
                        _lastItem = Service.Data.GetExcelSheet<Item>().GetRow(itemId);
                        args.AddMenuItem(_inventoryWikiItem);
                    }

                    break;
                }
                case "ChatLog":
                {
                    var agent = Service.Gui.FindAgentInterface("ChatLog");
                    if (agent == nint.Zero || !ValidateChatLogContext(agent))
                        return;
                    var itemId = *(uint*)(agent + ChatLogContextItemId);
                    if (HandleItem(itemId) && SearchCollector)
                    {
                        args.AddMenuItem(_inventoryCollectorItem);
                    }
                    
                    if (SearchWiki)
                    {
                        _lastItem = Service.Data.GetExcelSheet<Item>().GetRow(itemId);
                        args.AddMenuItem(_inventoryWikiItem);
                    }
                    break;
                }
            }
        }
    }

    private static void OnClick(MenuItemClickedArgs _)
    {
        if (_lastItem != null)
        {
            Util.OpenLink(string.Format(CollectorUrl, _lastItem.Name));
        }
    }

    private static void OnWikiClick(MenuItemClickedArgs _)
    {
        if (_lastItem != null)
        {
            Util.OpenLink(string.Format(WikiUrl, _lastItem.Name));
        }
    }

    private static bool HandleItem(uint id)
    {
        return Service.PresetData.EquipmentItems.TryGetValue(id, out _lastItem);
    }

    private static unsafe bool ValidateChatLogContext(nint agent)
        => *(uint*)(agent + ChatLogContextItemId + 8) == 3;

    public override void Uninit()
    {
        Service.ContextMenu.OnMenuOpened -= OnMenuOpened;
        base.Uninit();
    }
}
