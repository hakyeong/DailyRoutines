using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using DailyRoutines.Clicks;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Internal.Notifications;
using Dalamud.Interface.Utility;
using Dalamud.Memory;
using Dalamud.Plugin.Services;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoRetainerPriceAdjustTitle", "AutoRetainerPriceAdjustDescription", ModuleCategories.Retainer)]
public unsafe partial class AutoRetainerPriceAdjust : DailyModuleBase
{
    public class MarketSellItem
    {
        public string RetainerName { get; set; } = null!;
        public int Price { get; set; }
        public bool IsHQ { get; set; }
    }

    private static int ConfigPriceReduction;
    private static int ConfigLowestPrice;
    private static int ConfigMaxPriceReduction;
    private static bool ConfigSeparateNQAndHQ;

    private static int CurrentItemPrice;
    private static int CurrentMarketLowestPrice;
    private static uint CurrentItemSearchItemID;
    private static bool IsCurrentItemHQ;
    private static RetainerManager.Retainer* CurrentRetainer;
    private static readonly HashSet<string> PlayerRetainers = [];

    public override void Init()
    {
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 10000, ShowDebug = false };

        AddConfig(this, "PriceReduction", 1);
        AddConfig(this, "LowestAcceptablePrice", 100);
        AddConfig(this, "SeparateNQAndHQ", false);
        AddConfig(this, "MaxPriceReduction", 0);

        ConfigPriceReduction = GetConfig<int>(this, "PriceReduction");
        ConfigLowestPrice = GetConfig<int>(this, "LowestAcceptablePrice");
        ConfigSeparateNQAndHQ = GetConfig<bool>(this, "SeparateNQAndHQ");
        ConfigMaxPriceReduction = GetConfig<int>(this, "MaxPriceReduction");

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "RetainerList", OnRetainerList);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "RetainerList", OnRetainerList);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "RetainerSellList", OnRetainerSellList);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "RetainerSell", OnRetainerSell);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "RetainerSell", OnRetainerSell);

        Initialized = true;
    }

    public override void ConfigUI()
    {
        ConflictKeyText();

        ImGui.SetNextItemWidth(150f * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt(
                $"{Service.Lang.GetText("AutoRetainerPriceAdjust-SinglePriceReductionValue")}##AutoRetainerPriceAdjust-SinglePriceReductionValue",
                ref ConfigPriceReduction, 100))
        {
            ConfigPriceReduction = Math.Max(1, ConfigPriceReduction);
            UpdateConfig(this, "SinglePriceReductionValue", ConfigPriceReduction);
        }


        ImGui.SetNextItemWidth(150f * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt(
                $"{Service.Lang.GetText("AutoRetainerPriceAdjust-LowestAcceptablePrice")}##AutoRetainerPriceAdjust-LowestAcceptablePrice",
                ref ConfigLowestPrice, 100))
        {
            ConfigLowestPrice = Math.Max(1, ConfigLowestPrice);
            UpdateConfig(this, "LowestAcceptablePrice", ConfigLowestPrice);
        }

        ImGui.SetNextItemWidth(150f * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt(
                $"{Service.Lang.GetText("AutoRetainerPriceAdjust-MaxPriceReduction")}##AutoRetainerPriceAdjust-MaxPriceReduction",
                ref ConfigMaxPriceReduction, 100))
        {
            ConfigMaxPriceReduction = Math.Max(0, ConfigMaxPriceReduction);
            UpdateConfig(this, "MaxPriceReduction", ConfigMaxPriceReduction);
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Service.Lang.GetText("AutoRetainerPriceAdjust-MaxPriceReductionInputHelp"));

        if (ImGui.Checkbox(
                $"{Service.Lang.GetText("AutoRetainerPriceAdjust-SeparateNQAndHQ")}##AutoRetainerPriceAdjust-SeparateNQAndHQ",
                ref ConfigSeparateNQAndHQ))
            UpdateConfig(this, "SeparateNQAndHQ", ConfigSeparateNQAndHQ);

        ImGuiOm.HelpMarker(Service.Lang.GetText("AutoRetainerPriceAdjust-SeparateNQAndHQHelp"));
    }

    private void OnRetainerList(AddonEvent type, AddonArgs args)
    {
        switch (type)
        {
            case AddonEvent.PostSetup:
                if (InterruptByConflictKey()) return;

                var retainerManager = RetainerManager.Instance();
                if (retainerManager == null) return;

                PlayerRetainers.Clear();

                for (var i = 0U; i < retainerManager->GetRetainerCount(); i++)
                {
                    var retainer = retainerManager->GetRetainerBySortedIndex(i);
                    if (retainer == null) break;

                    var retainerName = MemoryHelper.ReadSeStringNullTerminated((nint)retainer->Name).ExtractText();
                    PlayerRetainers.Add(retainerName);
                }
                break;
            case AddonEvent.PreFinalize:
                CurrentRetainer = null;
                break;
        }
    }

    private void OnRetainerSell(AddonEvent eventType, AddonArgs addonInfo)
    {
        switch (eventType)
        {
            case AddonEvent.PostSetup:
                if (InterruptByConflictKey()) return;
                if (TaskManager.IsBusy) return;
                // 点击比价
                TaskManager.Enqueue(ClickComparePrice);
                TaskManager.AbortOnTimeout = false;
                TaskManager.DelayNext(500);
                // 获取当前最低价，并退出
                TaskManager.Enqueue(GetLowestPrice);
                TaskManager.AbortOnTimeout = true;
                TaskManager.DelayNext(100);
                // 填写最低价
                TaskManager.Enqueue(FillLowestPrice);
                break;
            case AddonEvent.PreFinalize:
                if (InterruptByConflictKey()) return;
                if (TaskManager.NumQueuedTasks <= 1)
                    TaskManager.Abort();
                break;
        }
    }

    private void OnRetainerSellList(AddonEvent type, AddonArgs args)
    {
        if (InterruptByConflictKey()) return;

        var activeRetainer = RetainerManager.Instance()->GetActiveRetainer();
        if (CurrentRetainer == null || CurrentRetainer != activeRetainer)
            CurrentRetainer = activeRetainer;
        else
            return;

        GetSellListItems(out var itemCount);
        if (itemCount == 0) return;

        for (var i = 0; i < itemCount; i++)
        {
            EnqueueSingleItem(i);
            CurrentMarketLowestPrice = 0;
        }
    }

    private void EnqueueSingleItem(int index)
    {
        if (InterruptByConflictKey()) return;

        // 点击物品
        TaskManager.Enqueue(() => ClickSellingItem(index));
        TaskManager.DelayNext(100);
        // 点击修改价格
        TaskManager.Enqueue(ClickAdjustPrice);
        TaskManager.DelayNext(100);
        // 点击比价
        TaskManager.Enqueue(ClickComparePrice);
        TaskManager.DelayNext(500);
        TaskManager.AbortOnTimeout = false;
        // 获取当前最低价，并退出
        TaskManager.Enqueue(GetLowestPrice);
        TaskManager.AbortOnTimeout = true;
        TaskManager.DelayNext(100);
        // 填写最低价
        TaskManager.Enqueue(FillLowestPrice);
        TaskManager.DelayNext(800);
    }

    private void GetSellListItems(out uint availableItems)
    {
        availableItems = 0;
        if (InterruptByConflictKey()) return;

        if (TryGetAddonByName<AtkUnitBase>("RetainerSellList", out var addon) && HelpersOm.IsAddonAndNodesReady(addon))
        {
            for (var i = 0; i < 20; i++)
                if (InventoryManager.Instance()->GetInventoryContainer(InventoryType.RetainerMarket)->GetInventorySlot(
                        i)->ItemID != 0)
                    availableItems++;
        }
    }

    private bool? ClickSellingItem(int index)
    {
        if (InterruptByConflictKey()) return true;

        if (TryGetAddonByName<AtkUnitBase>("RetainerSellList", out var addon) && HelpersOm.IsAddonAndNodesReady(addon))
        {
            AgentManager.SendEvent(AgentId.Retainer, 3, 0, index, 1);
            return true;
        }

        return false;
    }

    private bool? ClickAdjustPrice()
    {
        if (InterruptByConflictKey()) return true;

        if (TryGetAddonByName<AtkUnitBase>("ContextMenu", out var addon) && HelpersOm.IsAddonAndNodesReady(addon))
        {
            AgentManager.SendEvent(AgentId.Context, 0, 0, 0, 0, 0, 0);

            return true;
        }

        return false;
    }

    private bool? ClickComparePrice()
    {
        if (InterruptByConflictKey()) return true;

        if (TryGetAddonByName<AtkUnitBase>("RetainerSell", out var addon) && HelpersOm.IsAddonAndNodesReady(addon))
        {
            CurrentItemPrice = addon->AtkValues[5].Int;
            IsCurrentItemHQ = Marshal.PtrToStringUTF8((nint)addon->AtkValues[1].String).Contains(''); // HQ 符号

            var handler = new ClickRetainerSellDR((nint)addon);
            handler.ComparePrice();

            return true;
        }

        return false;
    }

    private bool? GetLowestPrice()
    {
        if (InterruptByConflictKey()) return true;

        if (TryGetAddonByName<AddonItemSearchResult>("ItemSearchResult", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(&addon->AtkUnitBase))
        {
            var ui = &addon->AtkUnitBase;
            if (!TryGetAddonByName<AtkUnitBase>("ItemHistory", out var historyAddon))
            {
                var handler = new ClickItemSearchResultDR();
                handler.History();
            }

            if (!HelpersOm.IsAddonAndNodesReady(historyAddon)) return false;

            CurrentItemSearchItemID = (uint)ui->AtkValues[0].Int;
            var errorMessage = addon->ErrorMessage->NodeText.ExtractText();
            if (errorMessage.Contains("没有搜索到任何结果"))
            {
                if (historyAddon->GetTextNodeById(11)->AtkResNode.IsVisible)
                {
                    CurrentMarketLowestPrice = 0;
                    ui->Close(true);
                    return true;
                }

                if (historyAddon->GetComponentListById(10)->ItemRendererList == null) return false;

                var result = ScanItemHistory(historyAddon);
                if (result.Any())
                {
                    if (ConfigSeparateNQAndHQ && IsCurrentItemHQ)
                    {
                        CurrentMarketLowestPrice = result.Where(x => x.HQ).OrderByDescending(x => x.Price)
                                                         .FirstOrDefault().Price;
                        if (CurrentMarketLowestPrice == 0)
                            CurrentMarketLowestPrice = result.OrderByDescending(x => x.Price).FirstOrDefault().Price;
                    }
                    else
                        CurrentMarketLowestPrice = result.OrderByDescending(x => x.Price).FirstOrDefault().Price;

                    ui->Close(true);
                    return true;
                }

                CurrentMarketLowestPrice = 0;
                ui->Close(true);
                return true;
            }

            if (addon->Results->ItemRendererList == null) return false;

            // 区分 HQ 和 NQ
            if (ConfigSeparateNQAndHQ && IsCurrentItemHQ)
            {
                if (!TryScanItemSearchResult(addon, out var result)) return false;

                var firstHQItem = result
                                  .OrderByDescending(x => x.IsHQ)
                                  .ThenByDescending(x => !PlayerRetainers.Contains(x.RetainerName))
                                  .ThenBy(x => x.Price)
                                  .FirstOrDefault();

                CurrentMarketLowestPrice = firstHQItem?.Price ?? 0;
            }
            else
            {
                if (!TryScanItemSearchResult(addon, out var result)) return false;

                var firstHQItem = result
                                  .OrderByDescending(x => !PlayerRetainers.Contains(x.RetainerName))
                                  .ThenBy(x => x.Price)
                                  .FirstOrDefault();

                CurrentMarketLowestPrice = firstHQItem?.Price ?? 0;
            }

            ui->Close(true);
            return true;
        }

        return false;
    }

    private bool? FillLowestPrice()
    {
        if (InterruptByConflictKey()) return true;

        if (TryGetAddonByName<AddonRetainerSell>("RetainerSell", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(&addon->AtkUnitBase))
        {
            var ui = &addon->AtkUnitBase;
            var priceComponent = addon->AskingPrice;
            var isOriginalPriceValid =
                int.TryParse(priceComponent->AtkComponentInputBase.AtkTextNode->NodeText.ExtractText(),
                             out var originalPrice);
            var handler = new ClickRetainerSellDR((nint)addon);

            if (isOriginalPriceValid && CurrentMarketLowestPrice - ConfigPriceReduction == originalPrice)
            {
                handler.Decline();
                ui->Close(true);
                ResetCurrentItemStats();

                return true;
            }

            // 低于最低价
            if (CurrentMarketLowestPrice - ConfigPriceReduction < ConfigLowestPrice)
            {
                var message = Service.Lang.GetSeString("AutoRetainerPriceAdjust-WarnMessageReachLowestPrice",
                                                       SeString.CreateItemLink(
                                                           CurrentItemSearchItemID,
                                                           IsCurrentItemHQ
                                                               ? ItemPayload.ItemKind.Hq
                                                               : ItemPayload.ItemKind.Normal), CurrentMarketLowestPrice,
                                                       CurrentItemPrice, ConfigLowestPrice);
                Service.Chat.Print(message);

                handler.Decline();
                ui->Close(true);
                ResetCurrentItemStats();

                return true;
            }

            // 超过可接受的降价值
            if (ConfigMaxPriceReduction != 0 && CurrentItemPrice - CurrentMarketLowestPrice > ConfigMaxPriceReduction)
            {
                var message = Service.Lang.GetSeString("AutoRetainerPriceAdjust-MaxPriceReductionMessage",
                                                       SeString.CreateItemLink(
                                                           CurrentItemSearchItemID,
                                                           IsCurrentItemHQ
                                                               ? ItemPayload.ItemKind.Hq
                                                               : ItemPayload.ItemKind.Normal), CurrentMarketLowestPrice,
                                                       CurrentItemPrice, ConfigMaxPriceReduction);
                Service.Chat.Print(message);

                handler.Decline();
                ui->Close(true);
                ResetCurrentItemStats();

                return true;
            }

            priceComponent->SetValue(CurrentMarketLowestPrice - ConfigPriceReduction);
            handler.Confirm();
            ui->Close(true);
            ResetCurrentItemStats();

            return true;
        }

        return false;
    }

    public bool TryScanItemSearchResult(
        AddonItemSearchResult* addon, out List<MarketSellItem> result)
    {
        result = [];
        if (addon == null) return false;

        var list = addon->Results->ItemRendererList;
        if (list == null) return false;
        var resultAmount = addon->Results->ListLength;

        for (var i = 0; i < resultAmount; i++)
        {
            var resultEntry = new MarketSellItem();

            var itemEntry = addon->Results->ItemRendererList[i].AtkComponentListItemRenderer;
            if (itemEntry == null) break;

            var listing = itemEntry->AtkComponentButton.AtkComponentBase;
            var stringArray = UIModule.Instance()->GetRaptureAtkModule()->AtkModule.AtkArrayDataHolder.StringArrays[33]->StringArray;

            var priceData = stringArray[203 + (6 * i)];
            var retainerData = stringArray[208 + (6 * i)];

            resultEntry.RetainerName = SanitizeManager.Sanitize(MemoryHelper.ReadStringNullTerminated((nint)retainerData));
            if (!int.TryParse(SanitizeManager.Sanitize(MemoryHelper.ReadStringNullTerminated((nint)priceData)).Replace(",", ""), out var priceResult)) return false;
            resultEntry.Price = priceResult;

            if (i < 10)
                resultEntry.IsHQ = listing.GetImageNodeById(3)->GetAsAtkImageNode()->AtkResNode.IsVisible;

            result.Add(resultEntry);
        }

        return true;
    }

    public static List<(bool HQ, int Price, int Amount)> ScanItemHistory(AtkUnitBase* addon)
    {
        var result = new List<(bool HQ, int Price, int Amount)>();
        var list = addon->GetComponentListById(10);

        for (var i = 0; i < list->ListLength; i++)
        {
            var listing = list->ItemRendererList[i].AtkComponentListItemRenderer->AtkComponentButton.AtkComponentBase
                .UldManager.NodeList;
            var isHQ = listing[8]->IsVisible;
            if (!int.TryParse(
                    SanitizeManager.Sanitize(listing[6]->GetAsAtkTextNode()->NodeText.ExtractText()).Replace(",", ""),
                    out var price)) continue;
            if (!int.TryParse(listing[5]->GetAsAtkTextNode()->NodeText.ExtractText(), out var amount)) continue;
            result.Add((isHQ, price, amount));
        }

        return result;
    }

    private static void ResetCurrentItemStats()
    {
        CurrentItemPrice = CurrentMarketLowestPrice = 0;
        CurrentItemSearchItemID = 0;
        IsCurrentItemHQ = false;
    }

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnRetainerList);
        Service.AddonLifecycle.UnregisterListener(OnRetainerSellList);
        Service.AddonLifecycle.UnregisterListener(OnRetainerSell);
        
        base.Uninit();
    }

    [GeneratedRegex("[^0-9]")]
    private static partial Regex AutoRetainerPriceAdjustRegex();
}
