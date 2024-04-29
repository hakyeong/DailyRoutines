using System.Collections.Generic;
using System.Linq;
using DailyRoutines.Helpers;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
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

    private static readonly MenuItem CollectorItem = new()
    {
        IsEnabled = true,
        IsReturn = false,
        UseDefaultPrefix = true,
        Name = new SeStringBuilder().Append(DRPrefix()).Append(Service.Lang.GetText("ExpandItemMenuSearch-CollectorSearch")).Build(),
        OnClicked = OnCollector,
        IsSubmenu = false,
        PrefixColor = 34
    };

    private static readonly MenuItem WikiItem = new()
    {
        IsEnabled = true,
        IsReturn = false,
        UseDefaultPrefix = true,
        Name = new SeStringBuilder().Append(DRPrefix()).Append(Service.Lang.GetText("ExpandItemMenuSearch-WikiSearch")).Build(),
        OnClicked = OnWiki,
        IsSubmenu = false,
        PrefixColor = 34
    };

    private static Item? _LastItem;
    private static Item? _LastPrismBoxItem;
    private static Item? _LastGlamourItem;
    private static ulong _LastHoveredItemID;
    private static ulong _LastDetailItemID;

    private static bool _IsOnCharacterInspect;
    private static bool _IsOnCabinetWithdraw;
    private static bool _IsOnMiragePrismPrismBoxCrystallize;
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
        AddConfig("SearchCollector", true);
        SearchCollector = GetConfig<bool>("SearchCollector");

        AddConfig("SearchCollectorByGlamour", true);
        SearchCollectorByGlamour = GetConfig<bool>("SearchCollectorByGlamour");

        AddConfig("SearchWiki", true);
        SearchWiki = GetConfig<bool>("SearchWiki");

        AddConfig("SearchWikiByGlamour", true);
        SearchWikiByGlamour = GetConfig<bool>("SearchWikiByGlamour");

        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = false };

        Service.ContextMenu.OnMenuOpened += OnMenuOpened;
        Service.Gui.HoveredItemChanged += OnHoveredItemChanged;
        Service.FrameworkManager.Register(OnUpdate);

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, "CharacterInspect", OnAddon);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize,
                                                ["CabinetWithdraw", "CharacterInspect,MiragePrismPrismBoxCrystallize"],
                                                OnAddon);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup,
                                                ["CabinetWithdraw", "MiragePrismPrismBoxCrystallize"], OnAddon);
        _CharacterInspectItems.Clear();
    }

    public override void ConfigUI()
    {
        if (ImGui.Checkbox(Service.Lang.GetText("ExpandItemMenuSearch-CollectorSearch"), ref SearchCollector))
            UpdateConfig("SearchCollector", SearchCollector);

        if (SearchCollector)
        {
            ImGui.Indent();
            ImGui.PushID("CollectorSearchGlamour");
            if (ImGui.Checkbox(Service.Lang.GetText("ExpandItemMenuSearch-SearchGlamour"),
                               ref SearchCollectorByGlamour))
                UpdateConfig("SearchCollectorByGlamour", SearchCollectorByGlamour);
            ImGui.PopID();
            ImGui.Unindent();
        }

        if (ImGui.Checkbox(Service.Lang.GetText("ExpandItemMenuSearch-WikiSearch"), ref SearchWiki))
            UpdateConfig("SearchWiki", SearchWiki);

        if (SearchWiki)
        {
            ImGui.Indent();
            ImGui.PushID("WikiSearchGlamour");
            if (ImGui.Checkbox(Service.Lang.GetText("ExpandItemMenuSearch-SearchGlamour"),
                               ref SearchWikiByGlamour))
                UpdateConfig("SearchWikiByGlamour", SearchWikiByGlamour);
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
                    case "MiragePrismPrismBoxCrystallize":
                        _IsOnMiragePrismPrismBoxCrystallize = true;
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
                    case "MiragePrismPrismBoxCrystallize":
                        _IsOnMiragePrismPrismBoxCrystallize = false;
                        break;
                }

                break;
        }
    }

    private static void OnHoveredItemChanged(object? sender, ulong id)
    {
        if (!_IsOnCharacterInspect && !_IsOnMiragePrismPrismBoxCrystallize) return;
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

            if (SearchCollector && PresetData.TryGetGear(itemId, out var itemCollector))
            {
                _LastItem = itemCollector;
                if (SearchCollectorByGlamour)
                    _LastGlamourItem = PresetData.TryGetGear(glamourId, out var glamourItem) ? glamourItem : _LastItem;

                args.AddMenuItem(CollectorItem);
            }

            if (SearchWiki && LuminaCache.TryGetRow<Item>(itemId, out var itemWiki))
            {
                _LastItem = itemWiki;
                if (SearchWikiByGlamour)
                    _LastGlamourItem = PresetData.TryGetGear(glamourId, out var glamourItem) ? glamourItem : _LastItem;

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
                if (SearchCollector && PresetData.Gears.TryGetValue(itemID, out var searchCollectorItem))
                {
                    _LastItem = searchCollectorItem;
                    args.AddMenuItem(CollectorItem);
                }

                if (SearchWiki && LuminaCache.TryGetRow<Item>(itemID, out var searchWikiItem))
                {
                    _LastItem = searchWikiItem;
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
                if (SearchCollector && PresetData.Gears.TryGetValue(itemID, out var collectorItem))
                {
                    _LastItem = collectorItem;
                    args.AddMenuItem(CollectorItem);
                }

                if (SearchWiki && LuminaCache.TryGetRow<Item>(itemID, out var wikiItem))
                {
                    _LastItem = wikiItem;
                    args.AddMenuItem(WikiItem);
                }

                break;
            }
            case "MiragePrismMiragePlate":
                _LastGlamourItem = null;

                var agentDetail = AgentMiragePrismPrismItemDetail.Instance();
                if (agentDetail == null) return;

                if (!PresetData.Gears.TryGetValue(agentDetail->ItemId, out var mirageItem)) return;
                _LastItem = mirageItem;

                if (SearchCollector) args.AddMenuItem(CollectorItem);
                if (SearchWiki) args.AddMenuItem(WikiItem);
                break;
            case "ColorantColoring":
                _LastGlamourItem = null;

                var agentColoring = AgentColorant.Instance();
                if (agentColoring == null) return;

                if (!PresetData.Dyes.TryGetValue(agentColoring->CharaView.SelectedStain, out var stainItem)) return;
                _LastItem = stainItem;

                if (SearchWiki) args.AddMenuItem(WikiItem);
                break;
            case "CabinetWithdraw":
                _LastGlamourItem = null;

                if (!PresetData.Gears.TryGetValue((uint)_LastDetailItemID, out var cabinetItem)) return;
                _LastItem = cabinetItem;

                if (SearchCollector) args.AddMenuItem(CollectorItem);
                if (SearchWiki) args.AddMenuItem(WikiItem);
                break;
            case "CharacterInspect":
            {
                var glamourID = _CharacterInspectItems.FirstOrDefault(x => x.ItemID == _LastHoveredItemID).GlamourID;

                if (!PresetData.Gears.TryGetValue((uint)_LastHoveredItemID, out var inspectItem)) return;
                _LastItem = inspectItem;
                _LastGlamourItem = PresetData.Gears.GetValueOrDefault(glamourID, _LastItem);

                if (SearchCollector) args.AddMenuItem(CollectorItem);
                if (SearchWiki) args.AddMenuItem(WikiItem);

                break;
            }
            case "MiragePrismPrismBoxCrystallize":
            {
                var itemId = AgentMiragePrismPrismItemDetail.Instance()->ItemId;
                PresetData.Gears.TryGetValue(itemId, out var miragePrismPrismBoxCrystallizeItem);
                PresetData.Gears.TryGetValue((uint)_LastHoveredItemID, out var miragePrismPrismBoxItem);
                if (miragePrismPrismBoxCrystallizeItem == null && miragePrismPrismBoxItem == null) return;

                _LastItem = miragePrismPrismBoxItem;
                _LastPrismBoxItem = miragePrismPrismBoxCrystallizeItem;
                _LastGlamourItem = null;
                if (SearchCollector) args.AddMenuItem(CollectorItem);
                if (SearchWiki) args.AddMenuItem(WikiItem);
                break;
            }
        }
    }

    private static void OnCollector(MenuItemClickedArgs args)
    {
        if (args.AddonName == "MiragePrismPrismBoxCrystallize" &&
            TryGetAddonByName<AtkUnitBase>("ContextMenu", out var addon) && IsAddonAndNodesReady(addon))
        {
            if (TryScanContextMenuText(addon, "投影到当前装备上", out var index))
                Util.OpenLink(string.Format(CollectorUrl, _LastPrismBoxItem.Name));
            else
                Util.OpenLink(string.Format(CollectorUrl, _LastItem.Name));

            return;
        }

        if (SearchCollectorByGlamour && _LastGlamourItem != null && _LastGlamourItem.Name.ToString().Length != 0)
            Util.OpenLink(string.Format(CollectorUrl, _LastGlamourItem.Name));
        else if (_LastItem != null)
            Util.OpenLink(string.Format(CollectorUrl, _LastItem.Name));
    }

    private static void OnWiki(MenuItemClickedArgs args)
    {
        if (args.AddonName == "MiragePrismPrismBoxCrystallize" &&
            TryGetAddonByName<AtkUnitBase>("ContextMenu", out var addon) && IsAddonAndNodesReady(addon))
        {
            if (TryScanContextMenuText(addon, "投影到当前装备上", out var index))
                Util.OpenLink(string.Format(CollectorUrl, _LastPrismBoxItem.Name));
            else
                Util.OpenLink(string.Format(CollectorUrl, _LastItem.Name));

            return;
        }

        if (SearchWikiByGlamour && _LastGlamourItem != null && _LastGlamourItem.Name.ToString().Length != 0)
            Util.OpenLink(string.Format(WikiUrl, _LastGlamourItem.Name));
        else if (_LastItem != null)
            Util.OpenLink(string.Format(WikiUrl, _LastItem.Name));
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
        Service.ContextMenu.OnMenuOpened -= OnMenuOpened;

        base.Uninit();
    }
}
