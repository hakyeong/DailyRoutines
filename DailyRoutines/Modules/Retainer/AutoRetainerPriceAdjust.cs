using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using ClickLib;
using ClickLib.Clicks;
using DailyRoutines.Helpers;
using DailyRoutines.Infos;
using DailyRoutines.Infos.Clicks;
using DailyRoutines.Managers;
using DailyRoutines.Windows;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Network.Structures;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Dalamud.Utility.Signatures;
using ECommons.Automation;
using ECommons.Configuration;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using Microsoft.Extensions.Caching.Memory;
using SeStringBuilder = Dalamud.Game.Text.SeStringHandling.SeStringBuilder;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoRetainerPriceAdjustTitle", "AutoRetainerPriceAdjustDescription", ModuleCategories.Retainer)]
public unsafe partial class AutoRetainerPriceAdjust : DailyModuleBase
{
    #region 预定义
    private static readonly InventoryType[] InventoryTypes =
    [
        InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3,
        InventoryType.Inventory4
    ];

    public enum AdjustBehavior
    {
        固定值,
        百分比,
    }

    [Flags]
    public enum AbortCondition
    {
        无 = 0,
        低于最小值 = 1,
        低于预期值 = 2,
        低于收购价 = 4,
        大于可接受降价值 = 5
    }

    public enum AbortBehavior
    {
        无,
        收回至雇员,
        收回至背包,
        出售至系统商店,
        改价至最小值,
        改价至预期值
    }

    public class ItemKey : IEquatable<ItemKey>
    {
        public uint ItemID { get; set; }
        public bool IsHQ { get; set; }

        public ItemKey() { }

        public ItemKey(uint itemID, bool isHQ)
        {
            ItemID = itemID;
            IsHQ = isHQ;
        }

        public override string ToString()
        {
            return $"{ItemID}_{(IsHQ ? "HQ" : "NQ")}";
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as ItemKey);
        }

        public bool Equals(ItemKey? other)
        {
            if (other is null || GetType() != other.GetType())
                return false;

            return ItemID == other.ItemID && IsHQ == other.IsHQ;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ItemID, IsHQ);
        }

        public static bool operator ==(ItemKey? lhs, ItemKey? rhs)
        {
            if (lhs is null)
            {
                return rhs is null;
            }
            return lhs.Equals(rhs);
        }

        public static bool operator !=(ItemKey lhs, ItemKey rhs)
        {
            return !(lhs == rhs);
        }
    }

    public class ItemConfig : IEquatable<ItemConfig>
    {
        public uint ItemID { get; set; }
        public bool IsHQ { get; set; }
        public string ItemName { get; set; } = string.Empty;

        /// <summary>
        /// 改价行为
        /// </summary>
        public AdjustBehavior AdjustBehavior { get; set; } = AdjustBehavior.固定值;
        /// <summary>
        /// 改价具体值
        /// </summary>
        public Dictionary<AdjustBehavior, int> AdjustValues { get; set; } = new()
        {
            { AdjustBehavior.固定值, 1 },
            { AdjustBehavior.百分比, 10 }
        };

        /// <summary>
        /// 最低可接受价格 (最小值: 1)
        /// </summary>
        public int PriceMinimum { get; set; } = 100;
        /// <summary>
        /// 预期价格 (最小值: PriceMinimum + 1)
        /// </summary>
        public int PriceExpected { get; set; } = 200;
        /// <summary>
        /// 最大可接受降价值 (设置为 0 以禁用)
        /// </summary>
        public int PriceMaxReduction { get; set; } = 0;

        /// <summary>
        /// 意外情况逻辑
        /// </summary>
        public Dictionary<AbortCondition, AbortBehavior> AbortLogic { get; set; } = [];

        public ItemConfig() { }

        public ItemConfig(uint itemID, bool isHQ)
        {
            ItemID = itemID;
            IsHQ = isHQ;
            ItemName = itemID == 0 ? Service.Lang.GetText("AutoRetainerPriceAdjust-CommonItemPreset") : LuminaCache.GetRow<Item>(ItemID).Name.RawString;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as ItemConfig);
        }

        public bool Equals(ItemConfig? other)
        {
            if (other is null || GetType() != other.GetType())
                return false;

            return ItemID == other.ItemID && IsHQ == other.IsHQ;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ItemID, IsHQ);
        }

        public static bool operator ==(ItemConfig? lhs, ItemConfig? rhs)
        {
            if (lhs is null)
            {
                return rhs is null;
            }
            return lhs.Equals(rhs);
        }

        public static bool operator !=(ItemConfig lhs, ItemConfig rhs)
        {
            return !(lhs == rhs);
        }
    }
    #endregion

    #region 游戏界面
    private static AtkUnitBase* SelectString => (AtkUnitBase*)Service.Gui.GetAddonByName("SelectString");
    private static AtkUnitBase* RetainerList => (AtkUnitBase*)Service.Gui.GetAddonByName("RetainerList");
    private static AtkUnitBase* RetainerSellList => (AtkUnitBase*)Service.Gui.GetAddonByName("RetainerSellList");
    private static AtkUnitBase* ItemSearchResult => (AtkUnitBase*)Service.Gui.GetAddonByName("ItemSearchResult");
    private static AtkUnitBase* ItemHistory => (AtkUnitBase*)Service.Gui.GetAddonByName("ItemHistory");
    private static InfoProxyItemSearch* InfoItemSearch => 
        (InfoProxyItemSearch*)InfoModule.Instance()->GetInfoProxyById(InfoProxyId.ItemSearch);
    #endregion

    #region 价格缓存
    private static readonly MemoryCache Cache = new(new MemoryCacheOptions());
    private static string GetPriceCacheKey(uint itemID, bool isHQ) => $"{itemID}_{(isHQ ? "HQ" : "NQ")}";

    private static bool TryGetPriceCache(uint itemID, bool isHQ, out uint price)
    {
        price = 0;

        var cacheKey = GetPriceCacheKey(itemID, isHQ);
        var result = Cache.Get<uint?>(cacheKey);
        if (result != null) price = (uint)result;
        return result != null;
    }

    private static void SetPriceCache(uint itemID, bool isHQ, uint price)
    {
        var cacheKey = GetPriceCacheKey(itemID, isHQ);
        Cache.Set(cacheKey, price, new MemoryCacheEntryOptions() {SlidingExpiration = TimeSpan.FromMinutes(5)});
    }
    #endregion

    private class Config : ModuleConfiguration
    {
        public readonly Dictionary<string, ItemConfig> ItemConfigs = new()
        {
            { new ItemKey(0, false).ToString(), new(0, false) },
            { new ItemKey(0, true).ToString(), new(0, true) },
        };

        public bool SendProcessMessage = true;
    }
    private static Config ModuleConfig = null!;

    private delegate nint MarketboardHistoryDelegate(nint a1, nint packetData);
    [Signature("40 53 48 83 EC ?? 48 8B 0D ?? ?? ?? ?? 48 8B DA E8 ?? ?? ?? ?? 48 85 C0 74 ?? 4C 8B 00 48 8B C8 41 FF 90 ?? ?? ?? ?? 48 8B C8 BA ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 85 C0 74 ?? 48 8D 53 ?? 41 B8 ?? ?? ?? ?? 48 8B C8 48 83 C4 ?? 5B E9 ?? ?? ?? ?? 48 83 C4 ?? 5B C3 CC CC CC CC CC CC CC CC CC CC 40 53", DetourName = nameof(MarketboardHistorDetour))]
    private readonly Hook<MarketboardHistoryDelegate>? MarketboardHistoryHook;

    private static Dictionary<string, Item>? ItemNames;
    private static Dictionary<string, Item> _ItemNames = [];
    private static Dictionary<uint, uint>? ItemsSellPrice;

    private static string PresetSearchInput = string.Empty;
    private static string ItemSearchInput = string.Empty;
    private static uint NewConfigItemID;
    private static bool NewConfigItemHQ;
    private static AbortCondition CondtionInput = AbortCondition.低于最小值;
    private static AbortBehavior BehaviorInput = AbortBehavior.无;

    private static ItemConfig? SelectedItemConfig;
    private static ItemKey? CurrentItem;
    private static int CurrentItemIndex = -1;
    private static List<MarketBoardHistory.MarketBoardHistoryListing>? ItemHistoryList;

    private static readonly HashSet<ulong> PlayerRetainers = [];

    public override void Init()
    {
        ModuleConfig = LoadConfig<Config>() ?? new();

        ItemsSellPrice ??= LuminaCache.Get<Item>()
                                  .Where(x => !string.IsNullOrEmpty(x.Name.RawString) && x.PriceLow != 0)
                                  .ToDictionary(x => x.RowId, x => x.PriceLow);

        ItemNames ??= LuminaCache.Get<Item>()
                                 .Where(x => !string.IsNullOrEmpty(x.Name.RawString) &&
                                             x.ItemSortCategory.Row != 3 && x.ItemSortCategory.Row != 4)
                                 .GroupBy(x => x.Name.RawString)
                                 .ToDictionary(x => x.Key, x => x.First());
        _ItemNames = ItemNames.Take(10).ToDictionary(x => x.Key, x => x.Value);

        // 出售品列表
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "RetainerSellList", OnRetainerSellList);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "RetainerSellList", OnRetainerSellList);
        // 雇员列表
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "RetainerList", OnRetainerList);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "RetainerList", OnRetainerList);
        // 出售详情
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "RetainerSell", OnRetainerSell);

        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 60000, ShowDebug = true };
        Overlay ??= new Overlay(this);

        Service.Hook.InitializeFromAttributes(this);
        MarketboardHistoryHook?.Enable();
    }

    #region UI
    public override void ConfigUI()
    {
        ConflictKeyText();

        if (ImGui.Checkbox(Service.Lang.GetText("AutoRetainerPriceAdjust-SendProcessMessage"),
                           ref ModuleConfig.SendProcessMessage))
            SaveConfig(ModuleConfig);

        ItemConfigSelector();

        ImGui.SameLine();
        ItemConfigEditor();
    }

    public override void OverlayUI()
    {
        var activeAddon = RetainerSellList != null ? RetainerSellList :
                          RetainerList != null ? RetainerList : null;
        if (activeAddon == null) return;

        var pos = new Vector2(activeAddon->GetX() - ImGui.GetWindowSize().X,
                              activeAddon->GetY() + 6);
        ImGui.SetWindowPos(pos);

        ImGui.BeginDisabled(activeAddon == RetainerSellList);
        ImGui.PushID("AdjustPrice-AllRetainers");
        ImGui.TextColored(ImGuiColors.DalamudYellow, Service.Lang.GetText("AutoRetainerPriceAdjust-AdjustForRetainers"));

        ImGui.Separator();

        ImGui.BeginDisabled(TaskManager.IsBusy);
        if (ImGui.Button(Service.Lang.GetText("Start"))) EnqueueAllRetainersInList();
        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("Stop"))) TaskManager.Abort();
        ImGui.PopID();
        ImGui.EndDisabled();

        ImGui.BeginDisabled(activeAddon == RetainerList);
        ImGui.PushID("AdjustPrice-SellList");
        ImGui.TextColored(ImGuiColors.DalamudYellow, Service.Lang.GetText("AutoRetainerPriceAdjust-AdjustForListItems"));

        ImGui.Separator();

        ImGui.BeginDisabled(TaskManager.IsBusy);
        if (ImGui.Button(Service.Lang.GetText("Start"))) EnqueueAllItemsInSellList();
        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("Stop"))) TaskManager.Abort();
        ImGui.PopID();
        ImGui.EndDisabled();
    }

    private void ItemConfigSelector()
    {
        var childSize = new Vector2(200 * ImGuiHelpers.GlobalScale, 300 * ImGuiHelpers.GlobalScale);

        if (ImGui.BeginChild("ItemConfigSelectorChild", childSize, true))
        {
            if (ImGuiOm.ButtonIcon("AddNewConfig", FontAwesomeIcon.Plus, Service.Lang.GetText("Add")))
                ImGui.OpenPopup("AddNewPreset");

            ImGui.SameLine();
            if (ImGuiOm.ButtonIcon("ImportConfig", FontAwesomeIcon.FileImport, Service.Lang.GetText("AutoRetainerPriceAdjust-ImportFromClipboard")))
            {
                var itemConfig = ImportItemConfigFromClipboard();
                if (itemConfig != null)
                {
                    var itemKey = new ItemKey(itemConfig.ItemID, itemConfig.IsHQ).ToString();
                    ModuleConfig.ItemConfigs[itemKey] = itemConfig;
                }
            }

            if (ImGui.BeginPopup("AddNewPreset"))
            {
                ImGui.SetNextItemWidth(150f * ImGuiHelpers.GlobalScale);
                ImGui.InputTextWithHint("###GameItemSearchInput", Service.Lang.GetText("PleaseSearch"), ref ItemSearchInput, 100);

                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    if (!string.IsNullOrWhiteSpace(ItemSearchInput) && ItemSearchInput.Length > 1)
                    {
                        _ItemNames = ItemNames.Where(x => x.Key.Contains(ItemSearchInput, StringComparison.OrdinalIgnoreCase)).ToDictionary(x => x.Key, x => x.Value);
                    }
                }

                ImGui.SameLine();
                ImGui.Checkbox("HQ", ref NewConfigItemHQ);

                ImGui.SameLine();
                if (ImGui.Button(Service.Lang.GetText("Confirm")))
                {
                    var newConfigStr = new ItemKey(NewConfigItemID, NewConfigItemHQ).ToString();
                    var newConfig = new ItemConfig(NewConfigItemID, NewConfigItemHQ);
                    if (ModuleConfig.ItemConfigs.TryAdd(newConfigStr, newConfig))
                    {
                        SaveConfig(ModuleConfig);
                        ImGui.CloseCurrentPopup();
                    }
                }

                ImGui.Separator();
                foreach (var (itemName, item) in _ItemNames)
                {
                    if (ImGuiOm.SelectableImageWithText(ImageHelper.GetIcon(item.Icon).ImGuiHandle, ImGuiHelpers.ScaledVector2(24f), itemName, item.RowId == NewConfigItemID, ImGuiSelectableFlags.DontClosePopups))
                        NewConfigItemID = item.RowId;
                }
                ImGui.EndPopup();
            }

            ImGui.SetNextItemWidth(-1f);
            ImGui.SameLine();
            ImGui.InputTextWithHint("###PresetSearchInput", Service.Lang.GetText("PleaseSearch"), ref PresetSearchInput, 100);

            ImGui.Separator();

            foreach (var itemConfig in ModuleConfig.ItemConfigs.ToList())
            {
                if (!string.IsNullOrWhiteSpace(PresetSearchInput) && !itemConfig.Value.ItemName.Contains(PresetSearchInput))
                    continue;

                if (ImGui.Selectable($"{itemConfig.Value.ItemName} {(itemConfig.Value.IsHQ ? "(HQ)" : "")}"))
                    SelectedItemConfig = itemConfig.Value;

                if (ImGui.BeginPopupContextItem($"{itemConfig.Value}_{itemConfig.Key}_{itemConfig.Value.ItemID}"))
                {
                    if (ImGui.Selectable(Service.Lang.GetText("AutoRetainerPriceAdjust-ExportToClipboard")))
                        ExportItemConfigToClipboard(itemConfig.Value);

                    if (itemConfig.Value.ItemID != 0)
                    {
                        if (ImGui.Selectable(Service.Lang.GetText("Delete")))
                        {
                            ModuleConfig.ItemConfigs.Remove(itemConfig.Key);
                            SaveConfig(ModuleConfig);

                            SelectedItemConfig = null;
                        }
                    }
                    ImGui.EndPopup();
                }

                if (itemConfig.Value is { ItemID: 0, IsHQ: true })
                    ImGui.Separator();
            }

            ImGui.EndChild();
        }
    }

    private void ItemConfigEditor()
    {
        var itemConfig = SelectedItemConfig;
        var childSize = new Vector2(450 * ImGuiHelpers.GlobalScale, 300 * ImGuiHelpers.GlobalScale);
        if (itemConfig == null)
        {
            if (ImGui.BeginChild("ItemConfigEditorChild", childSize, true))
                ImGui.EndChild();
            return;
        }

        // 基本信息获取
        var item = itemConfig.ItemID == 0 ? null : LuminaCache.GetRow<Item>(itemConfig.ItemID);
        var itemName = itemConfig.ItemID == 0 ? Service.Lang.GetText("AutoRetainerPriceAdjust-CommonItemPreset") : item.Name.RawString;
        var itemLogo = ImageHelper.GetIcon(
            itemConfig.ItemID == 0 ? 65002 : (uint)item.Icon,
            itemConfig.IsHQ ? ITextureProvider.IconFlags.ItemHighQuality : ITextureProvider.IconFlags.None);
        var itemBuyingPrice = itemConfig.ItemID == 0 ? 1 : item.PriceLow;


        if (ImGui.BeginChild("ItemConfigEditorChild", childSize, true))
        {
            // 物品基本信息展示
            PresetFont.Axis14.Push();
            ImGui.Image(itemLogo.ImGuiHandle, ImGuiHelpers.ScaledVector2(32));

            ImGui.SameLine();
            ImGui.SetWindowFontScale(1.3f * ImGuiHelpers.GlobalScale);
            ImGui.Text(itemName);

            ImGui.SameLine();
            ImGui.SetWindowFontScale(1.01f * ImGuiHelpers.GlobalScale);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (6f * ImGuiHelpers.GlobalScale));
            ImGui.Text(itemConfig.IsHQ ? $"({Service.Lang.GetText("HQ")})" : "");
            ImGui.SetWindowFontScale(1f);
            PresetFont.Axis14.Pop();

            ImGui.Separator();

            // 改价逻辑配置
            var radioButtonHeight = 24f;
            ImGui.BeginGroup();
            foreach (AdjustBehavior behavior in Enum.GetValues(typeof(AdjustBehavior)))
            {
                if (ImGui.RadioButton(behavior.ToString(), behavior == itemConfig.AdjustBehavior))
                {
                    itemConfig.AdjustBehavior = behavior;
                    SaveConfig(ModuleConfig);
                }

                radioButtonHeight = ImGui.GetItemRectSize().Y;
            }
            ImGui.EndGroup();

            ImGui.SameLine();
            ImGui.BeginGroup();
            if (itemConfig.AdjustBehavior == AdjustBehavior.固定值)
            {
                var originalValue = itemConfig.AdjustValues[AdjustBehavior.固定值];
                ImGui.SetNextItemWidth(100f * ImGuiHelpers.GlobalScale);
                ImGui.InputInt(Service.Lang.GetText("AutoRetainerPriceAdjust-ValueReduction"), ref originalValue);

                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    itemConfig.AdjustValues[AdjustBehavior.固定值] = originalValue;
                    SaveConfig(ModuleConfig);
                }
            }
            else
                ImGui.Dummy(new(radioButtonHeight));

            if (itemConfig.AdjustBehavior == AdjustBehavior.百分比)
            {
                var originalValue = itemConfig.AdjustValues[AdjustBehavior.百分比];
                ImGui.SetNextItemWidth(100f * ImGuiHelpers.GlobalScale);
                ImGui.InputInt(Service.Lang.GetText("AutoRetainerPriceAdjust-PercentageReduction"), ref originalValue);

                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    itemConfig.AdjustValues[AdjustBehavior.百分比] = Math.Clamp(originalValue, -99, 99);
                    SaveConfig(ModuleConfig);
                }
            }
            else
                ImGui.Dummy(new(radioButtonHeight));
            ImGui.EndGroup();

            ImGui.Dummy(ImGuiHelpers.ScaledVector2(10f));

            // 最低可接受价格与预期价格
            var originalMin = itemConfig.PriceMinimum;
            ImGui.SetNextItemWidth(100f * ImGuiHelpers.GlobalScale);
            ImGui.InputInt(Service.Lang.GetText("AutoRetainerPriceAdjust-PriceMinimum"), ref originalMin);

            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                itemConfig.PriceMinimum = Math.Max(1, originalMin);
                SaveConfig(ModuleConfig);
            }

            ImGui.SameLine();
            ImGui.BeginDisabled(itemConfig.ItemID == 0);
            if (ImGuiOm.ButtonIcon("ObtainBuyingPrice", FontAwesomeIcon.Store, Service.Lang.GetText("AutoRetainerPriceAdjust-ObtainBuyingPrice")))
            {
                itemConfig.PriceMinimum = Math.Max(1, (int)itemBuyingPrice);
                SaveConfig(ModuleConfig);
            }
            ImGui.EndDisabled();

            var originalExpected = itemConfig.PriceExpected;
            ImGui.SetNextItemWidth(100f * ImGuiHelpers.GlobalScale);
            ImGui.InputInt(Service.Lang.GetText("AutoRetainerPriceAdjust-PriceExpected"), ref originalExpected);

            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                itemConfig.PriceExpected = Math.Max(originalMin + 1, originalExpected);
                SaveConfig(ModuleConfig);
            }

            ImGui.SameLine();
            ImGui.BeginDisabled(itemConfig.ItemID == 0);
            if (ImGuiOm.ButtonIcon("OpenUniversalis", FontAwesomeIcon.Globe, Service.Lang.GetText("AutoRetainerPriceAdjust-OpenUniversalis")))
                Util.OpenLink($"https://universalis.app/market/{itemConfig.ItemID}");
            ImGui.EndDisabled();

            var originalPriceReducion = itemConfig.PriceMaxReduction;
            ImGui.SetNextItemWidth(100f * ImGuiHelpers.GlobalScale);
            ImGui.InputInt(Service.Lang.GetText("AutoRetainerPriceAdjust-PriceMaxReduction"), ref originalPriceReducion);

            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                itemConfig.PriceMaxReduction = Math.Max(0, originalPriceReducion);
                SaveConfig(ModuleConfig);
            }

            ImGui.Dummy(ImGuiHelpers.ScaledVector2(10f));

            // 意外情况
            ImGui.BeginGroup();
            ImGui.SetNextItemWidth(180f * ImGuiHelpers.GlobalScale);
            if (ImGui.BeginCombo("###AddNewLogicConditionCombo", CondtionInput.ToString(), ImGuiComboFlags.HeightLarge))
            {
                foreach (AbortCondition condition in Enum.GetValues(typeof(AbortCondition)))
                {
                    if (condition == AbortCondition.无) continue;
                    if (ImGui.Selectable(condition.ToString(), CondtionInput.HasFlag(condition), ImGuiSelectableFlags.DontClosePopups))
                    {
                        var combinedCondition = CondtionInput;
                        if (CondtionInput.HasFlag(condition))
                            combinedCondition &= ~condition;
                        else
                            combinedCondition |= condition;

                        CondtionInput = combinedCondition;
                    }
                }
                ImGui.EndCombo();
            }

            ImGui.SetNextItemWidth(180f * ImGuiHelpers.GlobalScale);
            if (ImGui.BeginCombo("###AddNewLogicBehaviorCombo", BehaviorInput.ToString(), ImGuiComboFlags.HeightLarge))
            {
                foreach (AbortBehavior behavior in Enum.GetValues(typeof(AbortBehavior)))
                {
                    if (ImGui.Selectable(behavior.ToString(), BehaviorInput == behavior, ImGuiSelectableFlags.DontClosePopups))
                        BehaviorInput = behavior;
                }
                ImGui.EndCombo();
            }
            ImGui.EndGroup();

            ImGui.SameLine();
            if (ImGuiOm.ButtonIconWithTextVertical(FontAwesomeIcon.Plus, Service.Lang.GetText("Add")))
            {
                if (CondtionInput != AbortCondition.无)
                {
                    itemConfig.AbortLogic.TryAdd(CondtionInput, BehaviorInput);
                    SaveConfig(ModuleConfig);
                }
            }

            ImGui.Separator();

            foreach (var logic in itemConfig.AbortLogic.ToList())
            {
                // 条件处理 (键)
                var origConditionStr = logic.Key.ToString();
                ImGui.SetNextItemWidth(200f * ImGuiHelpers.GlobalScale);
                ImGui.InputText($"###Condition_{origConditionStr}", ref origConditionStr, 100, ImGuiInputTextFlags.ReadOnly);

                if (ImGui.IsItemClicked())
                    ImGui.OpenPopup($"###ConditionSelectPopup_{origConditionStr}");

                if (ImGui.BeginPopup($"###ConditionSelectPopup_{origConditionStr}"))
                {
                    foreach (AbortCondition condition in Enum.GetValues(typeof(AbortCondition)))
                    {
                        if (ImGui.Selectable(condition.ToString(), logic.Key.HasFlag(condition)))
                        {
                            var combinedCondition = logic.Key;
                            if (logic.Key.HasFlag(condition))
                                combinedCondition &= ~condition;
                            else
                                combinedCondition |= condition;

                            if (!itemConfig.AbortLogic.ContainsKey(combinedCondition))
                            {
                                var origBehavior = logic.Value;
                                itemConfig.AbortLogic[combinedCondition] = origBehavior;
                                itemConfig.AbortLogic.Remove(logic.Key);
                                SaveConfig(ModuleConfig);
                            }
                        }
                    }
                    ImGui.EndPopup();
                }

                ImGui.SameLine();
                ImGui.Text("→");

                // 行为处理 (值)
                ImGui.SameLine();
                var origBehaviorStr = logic.Value.ToString();
                ImGui.SetNextItemWidth(150f * ImGuiHelpers.GlobalScale);
                ImGui.InputText($"###Behavior_{origBehaviorStr}", ref origBehaviorStr, 100, ImGuiInputTextFlags.ReadOnly);

                if (ImGui.IsItemClicked())
                    ImGui.OpenPopup($"###BehaviorSelectPopup_{origBehaviorStr}");

                if (ImGui.BeginPopup($"###BehaviorSelectPopup_{origBehaviorStr}"))
                {
                    foreach (AbortBehavior behavior in Enum.GetValues(typeof(AbortBehavior)))
                    {
                        if (ImGui.Selectable(behavior.ToString(), behavior == logic.Value))
                        {
                            itemConfig.AbortLogic[logic.Key] = behavior;
                            SaveConfig(ModuleConfig);
                        }
                    }
                    ImGui.EndPopup();
                }

                ImGui.SameLine();
                if (ImGuiOm.ButtonIcon($"Delete_{logic.Key}_{logic.Value}", FontAwesomeIcon.TrashAlt,
                                       Service.Lang.GetText("Delete")))
                    itemConfig.AbortLogic.Remove(logic.Key);
            }

            ImGui.EndChild();
        }
    }
    #endregion

    // 为所有雇员改价
    private void EnqueueAllRetainersInList()
    {
        var retainerManager = RetainerManager.Instance();

        for (var i = 0; i < PlayerRetainers.Count; i++)
        {
            var index = i;
            var marketItemCount = retainerManager->GetRetainerBySortedIndex((uint)i)->MarkerItemCount;
            if (marketItemCount <= 0) continue;

            TaskManager.Enqueue(() => ClickSpecificRetainer(index));
            TaskManager.Enqueue(ClickToEnterSellList);
            TaskManager.Enqueue(() =>
            {
                if (RetainerSellList == null) return false;
                
                for (var i1 = 0; i1 < marketItemCount; i1++)
                {
                    var index1 = i1;
                    TaskManager.Insert(() => ClickSellingItem(index1));
                    TaskManager.InsertDelayNext(500);
                }
                return true;
            });
            TaskManager.Enqueue(() =>
            {
                RetainerSellList->FireCloseCallback();
                RetainerSellList->Close(true);
            });
            TaskManager.Enqueue(ReturnToRetainerList);
        }
    }

    // 为当前列表下所有出售品改价
    private void EnqueueAllItemsInSellList()
    {
        var retainerManager = RetainerManager.Instance();
        var marketItemCount = retainerManager->GetActiveRetainer()->MarkerItemCount;

        if (marketItemCount == 0)
        {
            TaskManager.Abort();
            return;
        }

        for (var i = 0; i < marketItemCount; i++)
        {
            var index = i;
            TaskManager.Enqueue(() => ClickSellingItem(index));
            TaskManager.DelayNext(500);
        }
    }

    #region ClickOperations
    // 点击特定雇员
    private bool? ClickSpecificRetainer(int index)
    {
        if (InterruptByConflictKey()) return true;

        if(RetainerList == null || !IsAddonAndNodesReady(RetainerList)) return false;

        ClickRetainerList.Using((nint)RetainerList).Retainer(index);
        return true;
    }

    // 点击以进入出售列表
    private bool? ClickToEnterSellList()
    {
        if (InterruptByConflictKey()) return true;

        if (SelectString == null || !IsAddonAndNodesReady(SelectString)) return false;
        if (!TryScanSelectStringText(SelectString, LuminaCache.GetRow<Addon>(2383).Text.RawString, out var returnIndex) || !TryScanSelectStringText(SelectString, "出售", out var index))
        {
            TaskManager.Abort();
            if (returnIndex != -1) TaskManager.Enqueue(() => Click.TrySendClick($"select_string{returnIndex + 1}"));

            return true;
        }

        return Click.TrySendClick($"select_string{index + 1}");
    }

    // 返回雇员列表
    private bool? ReturnToRetainerList()
    {
        if (InterruptByConflictKey()) return true;
        if (RetainerList != null && IsAddonAndNodesReady(RetainerList)) return true;

        if (SelectString == null || !IsAddonAndNodesReady(SelectString)) return false;
        return ClickHelper.SelectString(LuminaCache.GetRow<Addon>(2383).Text.RawString);
    }

    // 点击列表中的物品
    private bool? ClickSellingItem(int index)
    {
        if (RetainerSellList == null || !IsAddonAndNodesReady(RetainerSellList)) return false;

        CurrentItemIndex = index;
        AgentHelper.SendEvent(AgentId.Retainer, 3, 0, index, 1);
        TaskManager.EnqueueImmediate(ClickAdjustPrice);

        return true;
    }

    // 点击右键菜单中的 修改价格
    private bool? ClickAdjustPrice()
    {
        if (InterruptByConflictKey()) return true;
        if (!ClickHelper.ContextMenu(LuminaCache.GetRow<Addon>(6948).Text.RawString)) return false;

        TaskManager.EnqueueImmediate(ClickComparePrice);
        return true;
    }

    // 点击比价按钮
    private bool? ClickComparePrice()
    {
        if (InterruptByConflictKey()) return true;

        if (!TryGetAddonByName<AtkUnitBase>("RetainerSell", out var addon) || !IsAddonAndNodesReady(addon)) return false;

        CurrentItem ??= new();
        CurrentItem.IsHQ = Marshal.PtrToStringUTF8((nint)addon->AtkValues[1].String).Contains(''); // HQ 符号

        ClickRetainerSell.Using((nint)addon).ComparePrice();

        TaskManager.DelayNextImmediate(1000);
        TaskManager.EnqueueImmediate(ObtainMarketData);
        return true;
    }

    // 获取市场数据
    private bool? ObtainMarketData()
    {
        if (!EzThrottler.Throttle("AutoRetainerPriceAdjust-ObtainMarketData", 1000)) return false;
        if (ItemSearchResult == null || !IsAddonAndNodesReady(ItemSearchResult)) return false;
        if (InfoItemSearch->SearchItemId == 0) return false;
        CurrentItem.ItemID = InfoItemSearch->SearchItemId;

        if (TryGetPriceCache(CurrentItem.ItemID, CurrentItem.IsHQ, out _))
        {
            ItemSearchResult->Close(true);
            TaskManager.EnqueueImmediate(FillPrice);

            return true;
        }

        if (ItemHistory == null) AddonHelper.Callback(ItemSearchResult, true, 0);
        if (!IsAddonAndNodesReady(ItemHistory)) return false;

        if (ItemHistoryList == null)
        {
            InfoItemSearch->RequestData();
            return false;
        }

        // 市场结果为空
        if (InfoItemSearch->ListingCount == 0)
        {
            // 历史结果为空
            if (ItemHistoryList.Count <= 0)
            {
                CloseAndEnqueue();
                return true;
            }

            var maxPrice = ItemHistoryList.DefaultIfEmpty().Max(x => x.SalePrice);
            var maxHQPrice = ItemHistoryList.Where(x => x is { IsHq: true, OnMannequin: false }).Max(x => x.SalePrice);

            if (maxPrice != 0)
                SetPriceCache(CurrentItem.ItemID, false, maxPrice);
            if (maxHQPrice != 0)
                SetPriceCache(CurrentItem.ItemID, true, maxHQPrice);

            CloseAndEnqueue();
            return true;
        }

        var listing = InfoItemSearch->Listings.ToArray();
        if (listing[0].ItemId != CurrentItem.ItemID) return false;

        var minPrice = listing
                       .Where(x => !PlayerRetainers.Contains(x.SellingRetainerContentId) &&
                                   x is { UnitPrice: > 0, IsHqItem: false })
                       .DefaultIfEmpty()
                       .Min(x => x.UnitPrice);
        var minHQPrice = listing
                         .Where(x => !PlayerRetainers.Contains(x.SellingRetainerContentId) &&
                                     x is { UnitPrice: > 0, IsHqItem: true })
                         .DefaultIfEmpty()
                         .Min(x => x.UnitPrice);

        if (minPrice > 0)
            SetPriceCache(CurrentItem.ItemID, false, minPrice);
        if (minHQPrice > 0)
            SetPriceCache(CurrentItem.ItemID, true, minHQPrice);


        CloseAndEnqueue();
        return true;

        void CloseAndEnqueue()
        {
            ItemHistory->Close(true);
            ItemSearchResult->Close(true);

            TaskManager.EnqueueImmediate(FillPrice);
        }
    }

    // 填入最低价格
    private bool? FillPrice()
    {
        if (InterruptByConflictKey()) return true;

        if (ItemSearchResult != null) return false;
        if (!TryGetAddonByName<AddonRetainerSell>("RetainerSell", out var addon) || 
            !IsAddonAndNodesReady(&addon->AtkUnitBase))
            return false;

        var ui = &addon->AtkUnitBase;

        var itemPreset = ModuleConfig.ItemConfigs.TryGetValue(CurrentItem.ToString(), out var _itemPreset) ? _itemPreset : ModuleConfig.ItemConfigs[new ItemKey(0, CurrentItem.IsHQ).ToString()];
        var origPrice = ui->AtkValues[5].Int;
        var itemPayload = new SeStringBuilder().AddItemLink(CurrentItem.ItemID, CurrentItem.IsHQ).Build();
        var retainerName = new SeStringBuilder()
                           .AddUiForeground(Marshal.PtrToStringUTF8((nint)RetainerManager.Instance()->GetActiveRetainer()->Name), 62).Build();
        if (!TryGetPriceCache(CurrentItem.ItemID, CurrentItem.IsHQ, out var marketPrice) &&
            !TryGetPriceCache(CurrentItem.ItemID, !CurrentItem.IsHQ, out marketPrice))
        {
            if (ModuleConfig.SendProcessMessage)
            {
                var message = new SeStringBuilder().Append(DRPrefix()).Append(
                    Service.Lang.GetSeString("AutoRetainerPriceAdjust-NoPriceDataFound",
                                             itemPayload, retainerName)).Build();
                Service.Chat.Print(message);
            }

            OperateAndReturn(false);
            return true;
        }

        var modifiedPrice = marketPrice;
        switch (itemPreset.AdjustBehavior)
        {
            case AdjustBehavior.固定值:
                modifiedPrice = (uint)((int)marketPrice - itemPreset.AdjustValues[AdjustBehavior.固定值]);
                break;
            case AdjustBehavior.百分比:
                modifiedPrice = (uint)(marketPrice * (1 - (itemPreset.AdjustValues[AdjustBehavior.百分比] / 100)));
                break;
        }

        // 价格不变
        if (modifiedPrice == origPrice)
        {
            OperateAndReturn(false);
            return true;
        }

        // 大于可接受降价值
        if (itemPreset.PriceMaxReduction != 0 &&
            Math.Abs(origPrice - modifiedPrice) > itemPreset.PriceMaxReduction &&
            itemPreset.AbortLogic.Keys.Any(x => x.HasFlag(AbortCondition.大于可接受降价值)))
        {
            var behavior = itemPreset.AbortLogic.FirstOrDefault(x => x.Key.HasFlag(AbortCondition.大于可接受降价值)).Value;
            NotifyAbortCondition(AbortCondition.大于可接受降价值);
            EnqueueAbortBehavior(behavior);
            return true;
        }

        // 低于收购价
        if (ItemsSellPrice.TryGetValue(CurrentItem.ItemID, out var buyingPrice) &&
            modifiedPrice < buyingPrice &&
            itemPreset.AbortLogic.Keys.Any(x => x.HasFlag(AbortCondition.低于收购价)))
        {
            var behavior = itemPreset.AbortLogic.FirstOrDefault(x => x.Key.HasFlag(AbortCondition.低于收购价)).Value;
            NotifyAbortCondition(AbortCondition.低于收购价);
            EnqueueAbortBehavior(behavior);
            return true;
        }

        // 低于最小值
        if (modifiedPrice < itemPreset.PriceMinimum &&
            itemPreset.AbortLogic.Keys.Any(x => x.HasFlag(AbortCondition.低于最小值)))
        {
            var behavior = itemPreset.AbortLogic.FirstOrDefault(x => x.Key.HasFlag(AbortCondition.低于最小值)).Value;
            NotifyAbortCondition(AbortCondition.低于最小值);
            EnqueueAbortBehavior(behavior);
            return true;
        }

        // 低于预期值
        if (modifiedPrice < itemPreset.PriceExpected &&
            itemPreset.AbortLogic.Keys.Any(x => x.HasFlag(AbortCondition.低于预期值)))
        {
            var behavior = itemPreset.AbortLogic.FirstOrDefault(x => x.Key.HasFlag(AbortCondition.低于预期值)).Value;
            NotifyAbortCondition(AbortCondition.低于预期值);
            EnqueueAbortBehavior(behavior);
            return true;
        }

        OperateAndReturn(true, modifiedPrice);
        return true;

        void OperateAndReturn(bool isConfirm, uint price = 0)
        {
            var priceComponent = addon->AskingPrice;
            var handler = new ClickRetainerSell();

            if (isConfirm && price != 0 && origPrice != price)
            {
                if (ModuleConfig.SendProcessMessage)
                {
                    var message = Service.Lang.GetSeString("AutoRetainerPriceAdjust-PriceAdjustSuccessfully", itemPayload, retainerName, origPrice, price);
                    Service.Chat.Print(new SeStringBuilder().Append(DRPrefix()).Append(message).Build());
                }
                priceComponent->SetValue((int)price);
                handler.Confirm();
            }
            else handler.Decline();

            ui->Close(true);
            ResetCurrentItemStats();
        }

        void EnqueueAbortBehavior(AbortBehavior behavior)
        {
            if (ModuleConfig.SendProcessMessage)
            {
                var behaviorMessage = new SeStringBuilder().AddUiForeground(behavior.ToString(), 67).Build();
                var message = Service.Lang.GetSeString("AutoRetainerPriceAdjust-ConductAbortBehavior", behaviorMessage);
                Service.Chat.Print(message);
            }

            if (behavior == AbortBehavior.无)
            {
                OperateAndReturn(false);
                return;
            }

            switch (behavior)
            {
                case AbortBehavior.改价至最小值:
                    OperateAndReturn(true, (uint)itemPreset.PriceMinimum);
                    break;
                case AbortBehavior.改价至预期值:
                    OperateAndReturn(true, (uint)itemPreset.PriceExpected);
                    break;
                case AbortBehavior.收回至雇员:
                    if (CurrentItemIndex != -1)
                    {
                        var copyIndex = CurrentItemIndex;
                        TaskManager.EnqueueImmediate(() =>
                        {
                            if (RetainerSellList == null || !IsAddonAndNodesReady(RetainerSellList)) return false;

                            AddonHelper.Callback(RetainerSellList, true, 0, copyIndex, 1);
                            return TryGetAddonByName<AtkUnitBase>("ContextMenu", out var cm) && IsAddonAndNodesReady(cm);
                        });

                        TaskManager.DelayNextImmediate(50);
                        TaskManager.EnqueueImmediate(() => ClickHelper.ContextMenu(LuminaCache.GetRow<Addon>(958).Text.RawString));
                    }
                    OperateAndReturn(false);
                    break;
                case AbortBehavior.收回至背包:
                    if (CurrentItemIndex != -1)
                    {
                        var copyIndex = CurrentItemIndex;
                        TaskManager.EnqueueImmediate(() =>
                        {
                            if (RetainerSellList == null || !IsAddonAndNodesReady(RetainerSellList)) return false;

                            AddonHelper.Callback(RetainerSellList, true, 0, copyIndex, 1);
                            return TryGetAddonByName<AtkUnitBase>("ContextMenu", out var cm) && IsAddonAndNodesReady(cm);
                        });

                        TaskManager.DelayNextImmediate(50);
                        TaskManager.EnqueueImmediate(() => ClickHelper.ContextMenu(LuminaCache.GetRow<Addon>(976).Text.RawString));
                    }
                    OperateAndReturn(false);
                    break;
                case AbortBehavior.出售至系统商店:
                    if (CurrentItemIndex != -1)
                    {
                        var copyIndex = CurrentItemIndex;
                        TaskManager.EnqueueImmediate(() =>
                        {
                            if (RetainerSellList == null || !IsAddonAndNodesReady(RetainerSellList)) return false;

                            AddonHelper.Callback(RetainerSellList, true, 0, copyIndex, 1);
                            return TryGetAddonByName<AtkUnitBase>("ContextMenu", out var cm) && IsAddonAndNodesReady(cm);
                        });
                        TaskManager.EnqueueImmediate(() => ClickHelper.ContextMenu(LuminaCache.GetRow<Addon>(976).Text.RawString));

                        TaskManager.DelayNextImmediate(500);
                        TaskManager.EnqueueImmediate(() =>
                        {
                            if (!TrySearchItemInInventory(CurrentItem.ItemID, CurrentItem.IsHQ, out var foundItem) || 
                                foundItem.Count <= 0) return true;

                            TaskManager.EnqueueImmediate(() => OpenInventoryItemContext(foundItem[0]));
                            TaskManager.EnqueueImmediate(() => ClickHelper.ContextMenu(
                                                             LuminaCache.GetRow<Addon>(5480).Text.RawString));

                            return true;
                        });
                    }
                    OperateAndReturn(false);
                    break;
            }
        }

        void NotifyAbortCondition(AbortCondition condition)
        {
            if (!ModuleConfig.SendProcessMessage) return;
            var conditionMessage = new SeStringBuilder().AddUiForeground(condition.ToString(), 60).Build();
            var message = Service.Lang.GetSeString("AutoRetainerPriceAdjust-DetectAbortCondition", itemPayload, retainerName, conditionMessage);
            Service.Chat.Print(new SeStringBuilder().Append(DRPrefix()).Append(message).Build());
        }
    }
    #endregion

    #region Helpers

    private static void ExportItemConfigToClipboard(ItemConfig config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config);
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

            Clipboard.SetText(base64);
            Service.Chat.Print(new SeStringBuilder().Append(DRPrefix()).Append($" 已成功导出 {config.ItemName} {(config.IsHQ ? "(HQ) " : "")}的配置至剪贴板").Build());
        }
        catch (Exception ex)
        {
            Service.Chat.PrintError($"导出至剪贴板错误: {ex.Message}");
        }
    }

    private static ItemConfig? ImportItemConfigFromClipboard()
    {
        try
        {
            var base64 = Clipboard.GetText();

            if (!string.IsNullOrEmpty(base64))
            {
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
                var config = JsonSerializer.Deserialize<ItemConfig>(json);
                if (config != null)
                    Service.Chat.Print(new SeStringBuilder().Append(DRPrefix()).Append($" 已成功导入 {config.ItemName} {(config.IsHQ ? "(HQ) " : "")}的配置").Build());
                return config;
            }
        }
        catch (Exception ex)
        {
            Service.Chat.PrintError($"从剪贴板导入配置时失败: {ex.Message}");
        }

        return null;
    }

    private static bool TrySearchItemInInventory(uint itemID, bool isHQ, out List<InventoryItem> foundItem)
    {
        foundItem = [];
        if (Service.Gui.GetAddonByName("InventoryExpansion") == nint.Zero) return false;
        var inventoryManager = InventoryManager.Instance();
        if (inventoryManager == null) return false;

        foreach (var type in InventoryTypes)
        {
            var container = inventoryManager->GetInventoryContainer(type);
            if (container == null) return false;

            for (var i = 0; i < container->Size; i++)
            {
                var slot = container->GetInventorySlot(i);
                if (slot->ItemID == itemID && (!isHQ || (isHQ && slot->Flags.HasFlag(InventoryItem.ItemFlags.HQ))))
                    foundItem.Add(*slot);
            }
        }

        return foundItem.Count > 0;
    }

    private static bool OpenInventoryItemContext(InventoryItem item)
    {
        if (!EzThrottler.Throttle("AutoDiscard", 100)) return false;
        var agent = AgentInventoryContext.Instance();
        if (agent == null) return false;

        agent->OpenForItemSlot(item.Container, item.Slot, GetActiveInventoryAddonID());
        return true;
    }

    private static uint GetActiveInventoryAddonID()
    {
        var inventory0 = (AtkUnitBase*)Service.Gui.GetAddonByName("Inventory");
        if (inventory0 == null) return 0;

        var inventory1 = (AtkUnitBase*)Service.Gui.GetAddonByName("InventoryLarge");
        if (inventory1 == null) return 0;

        var inventory2 = (AtkUnitBase*)Service.Gui.GetAddonByName("InventoryExpansion");
        if (inventory2 == null) return 0;

        if (IsAddonAndNodesReady(inventory0)) return inventory0->ID;
        if (IsAddonAndNodesReady(inventory1)) return inventory1->ID;
        if (IsAddonAndNodesReady(inventory2)) return inventory2->ID;

        return 0;
    }

    private static void ResetCurrentItemStats()
    {
        InfoItemSearch->SearchItemId = 0;
        InfoItemSearch->ListingCount = 0;
        InfoItemSearch->ClearData();

        ItemHistoryList = null;
        CurrentItem = null;
        CurrentItemIndex = -1;
    }
    #endregion

    #region Events
    // 历史交易数据获取
    private nint MarketboardHistorDetour(nint a1, nint packetData)
    {
        var data = MarketBoardHistory.Read(packetData);
        ItemHistoryList ??= data.HistoryListings;
        return MarketboardHistoryHook.Original(a1, packetData);
    }

    // 出售品列表 (悬浮窗控制)
    private void OnRetainerSellList(AddonEvent type, AddonArgs args)
    {
        Overlay.IsOpen = type switch
        {
            AddonEvent.PostSetup => true,
            AddonEvent.PreFinalize => false,
            _ => Overlay.IsOpen
        };
    }

    // 雇员列表 (获取其余雇员)
    private void OnRetainerList(AddonEvent type, AddonArgs args)
    {
        Overlay.IsOpen = type switch
        {
            AddonEvent.PostSetup => true,
            AddonEvent.PreFinalize => false,
            _ => Overlay.IsOpen
        };

        if (type == AddonEvent.PostSetup)
        {
            var retainerManager = RetainerManager.Instance();
            if (retainerManager == null) return;
            PlayerRetainers.Clear();

            for (var i = 0U; i < retainerManager->GetRetainerCount(); i++)
            {
                var retainer = retainerManager->GetRetainerBySortedIndex(i);
                if (retainer == null) break;

                PlayerRetainers.Add(retainer->RetainerID);
            }
        }
    }

    // 出售详情界面
    private void OnRetainerSell(AddonEvent eventType, AddonArgs addonInfo)
    {
        switch (eventType)
        {
            case AddonEvent.PostSetup:
                if (InterruptByConflictKey()) return;
                if (TaskManager.IsBusy) return;

                TaskManager.Enqueue(ClickComparePrice);
                break;
        }
    }
    #endregion

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnRetainerSellList);
        Service.AddonLifecycle.UnregisterListener(OnRetainerList);
        Service.AddonLifecycle.UnregisterListener(OnRetainerSell);

        Cache.Clear();

        base.Uninit();
    }

    [GeneratedRegex("[^0-9]")]
    private static partial Regex AutoRetainerPriceAdjustRegex();
}
