using System.Collections.Generic;
using System.Linq;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;

namespace DailyRoutines.Modules;

[ModuleDescription("ExpandItemMenuSearchTitle", "ExpandItemMenuSearchDescription", ModuleCategories.Interface)]
public unsafe class ExpandItemMenuSearch : DailyModuleBase
{
    public override string? Author { get; set; } = "HSS";

    private static Item? _LastItem;
    private static Item? _LastGlamourItem;
    private static ulong _LastHoveredItemID;
    private static ulong _LastDetailItemID;

    private static bool _IsOnCharacterInspect;
    private static bool _IsOnCabinetWithdraw;
    private static readonly HashSet<InventoryItem> _CharacterInspectItems = [];

    private static bool SearchCollector;
    private static bool SearchCollectorByGlamour;
    private static bool SearchWiki;
    private static bool SearchWikiByGlamour;

    private const int ChatLogContextItemId = 0x948;

    private const string CollectorUrl = "https://www.ffxivsc.cn/#/search?text={0}&type=armor";
    private const string WikiUrl = "https://ff14.huijiwiki.com/index.php?search={0}&profile=default&fulltext=1";

    public override void Init()
    {
        AddConfig(this, "SearchCollector", true);
        SearchCollector = GetConfig<bool>(this, "SearchCollector");

        AddConfig(this, "SearchCollectorByGlamour", true);
        SearchCollectorByGlamour = GetConfig<bool>(this, "SearchCollectorByGlamour");

        AddConfig(this, "SearchWiki", true);
        SearchWiki = GetConfig<bool>(this, "SearchWiki");

        AddConfig(this, "SearchWikiByGlamour", true);
        SearchWikiByGlamour = GetConfig<bool>(this, "SearchWikiByGlamour");

        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = false };

        Service.ContextMenu.OnMenuOpened += OnMenuOpened;
        Service.Gui.HoveredItemChanged += OnHoveredItemChanged;
        Service.Framework.Update += OnUpdate;

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, "CharacterInspect", OnAddon);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, ["CabinetWithdraw", "CharacterInspect"], OnAddon);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "CabinetWithdraw", OnAddon);
        _CharacterInspectItems.Clear();
    }

    public override void ConfigUI()
    {
        if (ImGui.Checkbox(Service.Lang.GetText("ExpandItemMenuSearch-CollectorSearch"), ref SearchCollector))
            UpdateConfig(this, "SearchCollector", SearchCollector);
        if (SearchCollector)
        {
            ImGui.Indent();
            ImGui.PushID("CollectorSearchGlamour");
            if (ImGui.Checkbox(Service.Lang.GetText("ExpandItemMenuSearch-SearchGlamour"),
                               ref SearchCollectorByGlamour))
                UpdateConfig(this, "SearchCollectorByGlamour", SearchCollectorByGlamour);
            ImGui.PopID();
            ImGui.Unindent();
        }

        if (ImGui.Checkbox(Service.Lang.GetText("ExpandItemMenuSearch-WikiSearch"), ref SearchWiki))
            UpdateConfig(this, "SearchWiki", SearchWiki);
        if (SearchWiki)
        {
            ImGui.Indent();
            ImGui.PushID("WikiSearchGlamour");
            if (ImGui.Checkbox(Service.Lang.GetText("ExpandItemMenuSearch-SearchGlamour"),
                               ref SearchWikiByGlamour))
                UpdateConfig(this, "SearchWikiByGlamour", SearchWikiByGlamour);
            ImGui.PopID();
            ImGui.Unindent();
        }
    }

    private void OnAddon(AddonEvent type, AddonArgs? args)
    {
        switch (type)
        {
            case AddonEvent.PostSetup:
                switch (args.AddonName)
                {
                    case "CabinetWithdraw":
                        _IsOnCabinetWithdraw = true;
                        break;
                }
                break;
            case AddonEvent.PostRefresh:
                switch (args.AddonName)
                {
                    case "CharacterInspect":
                        TaskManager.Enqueue(() =>
                        {
                            if (_CharacterInspectItems.Count != 0) return;
                            var container = InventoryManager.Instance()->GetInventoryContainer(InventoryType.Examine);
                            for (var i = 0; i < container->Size; i++)
                            {
                                var item = container->GetInventorySlot(i);
                                if (item == null || item->ItemID == 0) continue;

                                _CharacterInspectItems.Add(*item);
                            }

                            _IsOnCharacterInspect = true;
                        });
                        break;
                }
                break;
            case AddonEvent.PreFinalize:
                switch (args.AddonName)
                {
                    case "CabinetWithdraw":
                        _IsOnCabinetWithdraw = false;
                        break;
                    case "CharacterInspect":
                        TaskManager.Enqueue(() =>
                        {
                            _IsOnCharacterInspect = false;
                            _LastHoveredItemID = 0;
                            _CharacterInspectItems.Clear();
                        });
                        break;
                }
                break;
        }
    }

    private static void OnHoveredItemChanged(object? sender, ulong id)
    {
        if (!_IsOnCharacterInspect) return;
        var contextMenu = (AtkUnitBase*)Service.Gui.GetAddonByName("ContextMenu");
        if (contextMenu is null || !contextMenu->IsVisible)
        {
            if (id < 2000000) id %= 500000;
            if (id != 0 && _LastHoveredItemID != id)
                _LastHoveredItemID = id;
        }
    }

    private static void OnUpdate(IFramework framework)
    {
        if (!_IsOnCabinetWithdraw) return;
        var agent = AgentItemDetail.Instance();
        if (agent == null) return;

        var id = agent->ItemId;
        if (id != 0 && _LastDetailItemID != id)
            _LastDetailItemID = id;
    }

    private static void OnMenuOpened(MenuOpenedArgs args)
    {
        if (args.Target is MenuTargetInventory { TargetItem: not null } inventoryTarget)
        {
            var itemId = inventoryTarget.TargetItem.Value.ItemId;
            var glamourId = inventoryTarget.TargetItem.Value.GlamourId;

            if (SearchCollector)
            {
                if (SearchCollectorByGlamour)
                    TryGetItemByID(glamourId, out _LastGlamourItem);
                if (TryGetItemByID(itemId, out _LastItem))
                    args.AddMenuItem(CollectorItem);
            }

            if (SearchWiki)
            {
                if (SearchWikiByGlamour && glamourId != 0) TryGetItemByID(glamourId, out _LastGlamourItem);
                TryGetItemByID(itemId, out _LastItem);
                args.AddMenuItem(WikiItem);
            }

            return;
        }

        switch (args.AddonName)
        {
            case "ItemSearch" when args.AgentPtr != nint.Zero:
            {
                _LastGlamourItem = null;
                var itemID = (uint)AgentContext.Instance()->UpdateCheckerParam;
                if (TryGetItemByID(itemID, out _LastItem) && SearchCollector)
                    args.AddMenuItem(CollectorItem);

                if (SearchWiki)
                {
                    _LastItem = Service.Data.GetExcelSheet<Item>().GetRow(itemID);
                    args.AddMenuItem(WikiItem);
                }

                break;
            }
            case "ChatLog":
            {
                _LastGlamourItem = null;
                var agent = Service.Gui.FindAgentInterface("ChatLog");
                if (agent == nint.Zero || !IsValidChatLogContext(agent)) return;

                var itemID = *(uint*)(agent + ChatLogContextItemId);
                if (TryGetItemByID(itemID, out _LastItem) && SearchCollector) args.AddMenuItem(CollectorItem);

                if (SearchWiki)
                {
                    _LastItem = Service.Data.GetExcelSheet<Item>().GetRow(itemID);
                    args.AddMenuItem(WikiItem);
                }

                break;
            }
            case "MiragePrismMiragePlate":
                var agentDetail = AgentMiragePrismPrismItemDetail.Instance();
                if (agentDetail == null) return;

                if (!TryGetItemByID(agentDetail->ItemId, out _LastItem)) return;
                _LastGlamourItem = _LastItem;

                if (SearchCollector) args.AddMenuItem(CollectorItem);
                if (SearchWiki) args.AddMenuItem(WikiItem);
                break;
            case "ColorantColoring":
                var agentColoring = AgentColorant.Instance();
                if (agentColoring == null) return;

                if (!Service.PresetData.Dyes.TryGetValue(agentColoring->CharaView.SelectedStain, out var stainItemID))
                    return;
                if (!TryGetItemByID(stainItemID, out _LastItem)) return;

                if (SearchWiki) args.AddMenuItem(WikiItem);
                break;
            case "CabinetWithdraw":
                if (!TryGetItemByID((uint)_LastDetailItemID, out _LastGlamourItem)) return;
                _LastItem = _LastGlamourItem;

                if (SearchCollector) args.AddMenuItem(CollectorItem);
                if (SearchWiki) args.AddMenuItem(WikiItem);
                break;
            case "CharacterInspect":
            {
                var glamourID = _CharacterInspectItems
                                .FirstOrDefault(x => x.ItemID == _LastHoveredItemID).GlamourID;
                if (glamourID == 0)
                    TryGetItemByID((uint)_LastHoveredItemID, out _LastGlamourItem);
                else
                    TryGetItemByID(glamourID, out _LastGlamourItem);
                TryGetItemByID((uint)_LastHoveredItemID, out _LastItem);

                if (SearchCollector) args.AddMenuItem(CollectorItem);
                if (SearchWiki) args.AddMenuItem(WikiItem);

                break;
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
        if (SearchCollectorByGlamour && _LastGlamourItem != null && _LastGlamourItem.Name.ToString().Length != 0)
            Util.OpenLink(string.Format(CollectorUrl, _LastGlamourItem.Name));
        else if (_LastItem != null)
            Util.OpenLink(string.Format(CollectorUrl, _LastItem.Name));
    }

    private static void OnWiki(MenuItemClickedArgs _)
    {
        if (SearchWikiByGlamour && _LastGlamourItem != null && _LastGlamourItem.Name.ToString().Length != 0)
            Util.OpenLink(string.Format(WikiUrl, _LastGlamourItem.Name));
        else if (_LastItem != null)
            Util.OpenLink(string.Format(WikiUrl, _LastItem.Name));
    }

    private static bool TryGetItemByID(uint id, out Item? item)
    {
        item = Service.Data.GetExcelSheet<Item>().GetRow(id);
        return item != null;
    }

    private static bool IsValidChatLogContext(nint agent)
    {
        return *(uint*)(agent + ChatLogContextItemId + 8) == 3;
    }

    public override void Uninit()
    {
        _CharacterInspectItems.Clear();
        _LastGlamourItem = null;
        _LastItem = null;

        Service.AddonLifecycle.UnregisterListener(OnAddon);
        Service.Gui.HoveredItemChanged -= OnHoveredItemChanged;
        Service.Framework.Update -= OnUpdate;
        Service.ContextMenu.OnMenuOpened -= OnMenuOpened;

        base.Uninit();
    }
}
