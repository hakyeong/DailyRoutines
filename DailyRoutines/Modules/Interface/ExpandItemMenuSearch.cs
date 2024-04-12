using System;
using System.Reflection;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Inventory;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using Microsoft.VisualBasic.Devices;

namespace DailyRoutines.Modules;

[ModuleDescription("ExpandItemMenuSearchTitle", "ExpandItemMenuSearchDescription", ModuleCategories.Interface)]
public class ExpandItemMenuSearch : DailyModuleBase
{
    public override string? Author { get; set; } = "HSS";

    private static Item? _LastItem;
    private static Item? _LastGlamourItem;
    private static ulong _LastHoveredItemId;
    private static bool _CharacterInspectStatus;
    private static bool SearchCollector;
    private static bool SearchCollector_GlamourId;
    private static bool SearchWiki;
    private const int ChatLogContextItemId = 0x948;
    private const long CharacterInspectItemId = 0x109EB7360;
    private const long CharacterInspectGlamourHexValue = 0x109EB7388;

    private const string CollectorUrl = "https://www.ffxivsc.cn/#/search?text={0}&type=armor";
    private const string WikiUrl = "https://ff14.huijiwiki.com/index.php?search={0}&profile=default&fulltext=1";

    public override void Init()
    {
        AddConfig(this, "SearchCollector", true);
        AddConfig(this, "SearchCollector-GlamourId", true);
        AddConfig(this, "SearchWiki", true);
        SearchCollector = GetConfig<bool>(this, "SearchCollector");
        SearchCollector_GlamourId = GetConfig<bool>(this, "SearchCollector-GlamourId");
        SearchWiki = GetConfig<bool>(this, "SearchWiki");
        Service.ContextMenu.OnMenuOpened += OnMenuOpened;
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "CharacterInspect", OnCharacterInspect);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "CharacterInspect", OnCharacterInspect);
    }

    public override void ConfigUI()
    {
        if (ImGui.Checkbox(Service.Lang.GetText("ExpandItemMenuSearch-CollectorSearch"), ref SearchCollector))
            UpdateConfig(this, "SearchCollector", SearchCollector);
        if (SearchCollector)
        {
            ImGui.Indent();
            if (ImGui.Checkbox(Service.Lang.GetText("ExpandItemMenuSearch-CollectorSearchGlamourId"),
                               ref SearchCollector_GlamourId))
                UpdateConfig(this, "SearchCollector-GlamourId", SearchCollector_GlamourId);
            ImGui.Unindent();
        }

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
                var itemId = arg.TargetItem.Value.ItemId;
                if (SearchWiki)
                {
                    _LastItem = Service.Data.GetExcelSheet<Item>().GetRow(arg.TargetItem.Value.ItemId);
                    args.AddMenuItem(WikiItem);
                }

                if (SearchCollector)
                {
                    if (SearchCollector_GlamourId)
                    {
                        _LastGlamourItem = Service.Data.GetExcelSheet<Item>().GetRow(arg.TargetItem.Value.GlamourId);
                    }
                    if (TryGetItemByID(itemId) && SearchCollector) args.AddMenuItem(CollectorItem);
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
                case "CharacterInspect":
                {
                    
                    Service.Gui.HoveredItemChanged -= OnHoveredItemChanged;
                    if (SearchWiki)
                    {
                        _LastItem = Service.Data.GetExcelSheet<Item>().GetRow((uint)_LastHoveredItemId);
                        args.AddMenuItem(WikiItem);
                    }

                    break;
                }
            }
        }
    }

    private static void OnCharacterInspect(AddonEvent type, AddonArgs args)
    {
        if (type == AddonEvent.PostSetup && !_CharacterInspectStatus)
        {
            _CharacterInspectStatus = true;
            Service.Gui.HoveredItemChanged += OnHoveredItemChanged;
        }

        if (type == AddonEvent.PreFinalize && Service.Gui.GetAddonByName("CharacterInspect") == IntPtr.Zero)
        {
            _CharacterInspectStatus = false;
            _LastHoveredItemId = 0;
            Service.Gui.HoveredItemChanged -= OnHoveredItemChanged;
        }
    }

    private static unsafe void OnHoveredItemChanged(object? sender, ulong id)
    {
        if (id < 2000000) id %= 500000;
        if (id != 0 && _LastHoveredItemId != id)
        {
            _LastHoveredItemId = id;
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
        if (SearchCollector_GlamourId && _LastGlamourItem != null)
        {
            Util.OpenLink(string.Format(CollectorUrl, _LastGlamourItem.Name));
        }
        else if (_LastItem != null)
        {
            Util.OpenLink(string.Format(CollectorUrl, _LastItem.Name));
        }
    }

    private static void OnWiki(MenuItemClickedArgs _)
    {
        if (_LastItem != null) Util.OpenLink(string.Format(WikiUrl, _LastItem.Name));
        if (Service.Gui.GetAddonByName("CharacterInspect") != IntPtr.Zero)
        {
            Service.Gui.HoveredItemChanged += OnHoveredItemChanged;
        }
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
