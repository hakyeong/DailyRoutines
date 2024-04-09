using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using ClickLib;
using ClickLib.Clicks;
using DailyRoutines.Clicks;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using DailyRoutines.Windows;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using ECommons.Automation;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoRetainerPriceAdjustTitle", "AutoRetainerPriceAdjustDescription", ModuleCategories.Retainer)]
public unsafe partial class AutoRetainerPriceAdjust : DailyModuleBase
{
    private static AtkUnitBase* AddonRetainerSellList => (AtkUnitBase*)Service.Gui.GetAddonByName("RetainerSellList");
    private static AtkUnitBase* AddonRetainerList => (AtkUnitBase*)Service.Gui.GetAddonByName("RetainerList");
    private static AtkUnitBase* AddonItemSearchResult => (AtkUnitBase*)Service.Gui.GetAddonByName("ItemSearchResult");
    private static AtkUnitBase* AddonItemHistory => (AtkUnitBase*)Service.Gui.GetAddonByName("ItemHistory");

    private static InfoProxyItemSearch* InfoItemSearch =>
        (InfoProxyItemSearch*)InfoModule.Instance()->GetInfoProxyById(InfoProxyId.ItemSearch);

    private static int ConfigPriceReduction;
    private static int ConfigLowestPrice;
    private static int ConfigMaxPriceReduction;
    private static bool ConfigSeparateNQAndHQ;
    private static Dictionary<(uint Id, bool HQ), int> PriceCache = [];

    private static bool IsCurrentItemHQ;

    private static uint CurrentItemSearchItemID;

    private static readonly HashSet<ulong> PlayerRetainers = [];

    public override void Init()
    {
        #region Config

        AddConfig(this, "PriceReduction", 1);
        ConfigPriceReduction = GetConfig<int>(this, "PriceReduction");

        AddConfig(this, "LowestAcceptablePrice", 100);
        ConfigLowestPrice = GetConfig<int>(this, "LowestAcceptablePrice");

        AddConfig(this, "SeparateNQAndHQ", false);
        ConfigSeparateNQAndHQ = GetConfig<bool>(this, "SeparateNQAndHQ");

        AddConfig(this, "MaxPriceReduction", 0);
        ConfigMaxPriceReduction = GetConfig<int>(this, "MaxPriceReduction");

        #endregion

        // 出售品列表
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "RetainerSellList", OnRetainerSellList);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "RetainerSellList", OnRetainerSellList);
        // 雇员列表
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "RetainerList", OnRetainerList);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "RetainerList", OnRetainerList);

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "RetainerSell", OnRetainerSell);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "RetainerSell", OnRetainerSell);

        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 60000, ShowDebug = false };
        Overlay ??= new Overlay(this);
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

    public override void OverlayUI()
    {
        var activeAddon = AddonRetainerSellList != null ? AddonRetainerSellList :
                          AddonRetainerList != null ? AddonRetainerList : null;
        if (activeAddon == null) return;

        var pos = new Vector2(activeAddon->GetX() - ImGui.GetWindowSize().X,
                              activeAddon->GetY() + 6);
        ImGui.SetWindowPos(pos);

        ImGui.BeginDisabled(activeAddon == AddonRetainerSellList);
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

        ImGui.BeginDisabled(activeAddon == AddonRetainerList);
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

        ImGui.TextColored(ImGuiColors.DalamudYellow, Service.Lang.GetText("Settings"));

        ImGui.Separator();

        ConfigUI();
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
            PriceCache = [];
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
            case AddonEvent.PreFinalize:
                if (InterruptByConflictKey()) return;
                if (TaskManager.NumQueuedTasks <= 1)
                    TaskManager.Abort();
                break;
        }
    }

    // 为所有雇员改价
    private void EnqueueAllRetainersInList()
    {
        for (var i = 0; i < PlayerRetainers.Count; i++)
        {
            var index = i;
            TaskManager.Enqueue(() => ClickSpecificRetainer(index));
            TaskManager.Enqueue(ClickToEnterSellList);
            TaskManager.DelayNext(1000);
            TaskManager.Enqueue(() =>
            {
                if (AddonRetainerSellList == null) return false;
                var itemAmount = GetSellListItemAmount();
                if (itemAmount == 0)
                {
                    AddonRetainerSellList->FireCloseCallback();
                    AddonRetainerSellList->Close(true);

                    TaskManager.Insert(ReturnToRetainerList);
                    return true;
                }

                for (var i = 0; i < itemAmount; i++)
                {
                    var index = i;
                    TaskManager.Insert(() => ClickSellingItem(index));
                }
                return true;
            });
            TaskManager.Enqueue(() =>
            {
                AddonRetainerSellList->FireCloseCallback();
                AddonRetainerSellList->Close(true);
            });
            TaskManager.Enqueue(ReturnToRetainerList);
        }
    }

    // 为当前列表下所有出售品改价
    private void EnqueueAllItemsInSellList()
    {
        var itemAmount = GetSellListItemAmount();
        if (itemAmount == 0)
        {
            TaskManager.Abort();
            return;
        }

        for (var i = 0; i < itemAmount; i++)
        {
            var index = i;
            TaskManager.Enqueue(() => ClickSellingItem(index));
            TaskManager.DelayNext(500);
        }
    }

    // 点击特定雇员
    private bool? ClickSpecificRetainer(int index)
    {
        if (InterruptByConflictKey()) return true;

        if (TryGetAddonByName<AddonRetainerList>("RetainerList", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(&addon->AtkUnitBase))
        {
            var handler = new ClickRetainerList();
            handler.Retainer(index);
            return true;
        }

        return false;
    }

    // 点击以进入出售列表
    private bool? ClickToEnterSellList()
    {
        if (InterruptByConflictKey()) return true;

        if (TryGetAddonByName<AtkUnitBase>("SelectString", out var addon) && IsAddonReady(addon))
        {
            if (!HelpersOm.TryScanSelectStringText(addon, "返回", out var returnIndex) ||
                !HelpersOm.TryScanSelectStringText(addon, "出售", out var index))
            {
                TaskManager.Abort();
                if (returnIndex != -1) TaskManager.Enqueue(() => Click.TrySendClick($"select_string{returnIndex + 1}"));

                return true;
            }

            if (Click.TrySendClick($"select_string{index + 1}")) return true;
        }

        return false;
    }

    // 返回雇员列表
    private bool? ReturnToRetainerList()
    {
        if (InterruptByConflictKey()) return true;

        if (TryGetAddonByName<AddonRetainerList>("RetainerList", out var Laddon) &&
            HelpersOm.IsAddonAndNodesReady(&Laddon->AtkUnitBase))
        {
            return true;
        }

        if (TryGetAddonByName<AtkUnitBase>("SelectString", out var addon) && IsAddonReady(addon))
        {
            if (!HelpersOm.TryScanSelectStringText(addon, "返回", out var index))
            {
                TaskManager.Abort();
                return true;
            }

            if (Click.TrySendClick($"select_string{index + 1}")) return true;
        }

        return false;
    }

    // 点击列表中的物品
    private bool? ClickSellingItem(int index)
    {
        if (TryGetAddonByName<AtkUnitBase>("RetainerSellList", out var addon) && HelpersOm.IsAddonAndNodesReady(addon))
        {
            AgentManager.SendEvent(AgentId.Retainer, 3, 0, index, 1);

            TaskManager.EnqueueImmediate(ClickAdjustPrice);
            return true;
        }

        return false;
    }

    // 点击右键菜单中的 修改价格
    private bool? ClickAdjustPrice()
    {
        if (InterruptByConflictKey()) return true;

        if (TryGetAddonByName<AtkUnitBase>("ContextMenu", out var addon) && HelpersOm.IsAddonAndNodesReady(addon))
        {
            if (!HelpersOm.TryScanContextMenuText(addon, "修改价格", out var index)) return true;
            AgentManager.SendEvent(AgentId.Context, 0, 0, index, 0U, 0, 0);

            TaskManager.EnqueueImmediate(ClickComparePrice);
            return true;
        }

        return false;
    }

    // 点击比价按钮
    private bool? ClickComparePrice()
    {
        if (InterruptByConflictKey()) return true;

        if (TryGetAddonByName<AtkUnitBase>("RetainerSell", out var addon) && HelpersOm.IsAddonAndNodesReady(addon))
        {
            IsCurrentItemHQ = Marshal.PtrToStringUTF8((nint)addon->AtkValues[1].String).Contains(''); // HQ 符号

            AddonManager.Callback(addon, true, 4);

            TaskManager.DelayNextImmediate(200);
            TaskManager.EnqueueImmediate(ObtainMarketData);
            return true;
        }

        return false;
    }

    // 获取市场数据
    private bool? ObtainMarketData()
    {
        if (!EzThrottler.Throttle("AutoRetainerPriceAdjust-ObtainMarketData")) return false;
        if (AddonItemSearchResult == null || !HelpersOm.IsAddonAndNodesReady(AddonItemSearchResult)) return false;
        if (InfoItemSearch->SearchItemId != 0 && PriceCache.Count != 0)
        {
            if (PriceCache.TryGetValue((InfoItemSearch->SearchItemId, IsCurrentItemHQ), out var cachePrice) && cachePrice != 0)
            {
                AddonItemSearchResult->Close(true);
                TaskManager.EnqueueImmediate(() => FillLowestPrice(cachePrice));
                return true;
            }
        }

        if (AddonItemHistory == null) AddonManager.Callback(AddonItemSearchResult, true, 0);
        if (!HelpersOm.IsAddonAndNodesReady(AddonItemHistory)) return false;

        var errorMessage = AddonItemSearchResult->GetTextNodeById(5);
        var messages = errorMessage->NodeText.ExtractText();
        if (messages.Contains("没有搜索到任何结果"))
        {
            // 历史结果为空
            if (AddonItemHistory->GetTextNodeById(11)->AtkResNode.IsVisible)
            {
                SetMarkPriceValueAndReturn(0);
                return true;
            }

            if (AddonItemHistory->GetComponentListById(10)->ItemRendererList == null) return false;

            var result = ScanItemHistory(AddonItemHistory);
            if (result.Count != 0)
            {
                uint finalPrice;
                if (ConfigSeparateNQAndHQ && IsCurrentItemHQ)
                {
                    finalPrice = result.Where(x => x.HQ).OrderByDescending(x => x.Price)
                                       .FirstOrDefault().Price;
                    if (finalPrice == 0)
                        finalPrice = result.OrderByDescending(x => x.Price).FirstOrDefault().Price;
                }
                else
                    finalPrice = result.OrderByDescending(x => x.Price).FirstOrDefault().Price;

                SetMarkPriceValueAndReturn(finalPrice);
                return true;
            }

            SetMarkPriceValueAndReturn(0);
            return true;
        }
        if (messages.Contains("请稍后") && errorMessage->AtkResNode.IsVisible) return false;

        if (((AddonItemSearchResult*)AddonItemSearchResult)->Results->ItemRendererList == null) return false;
        if (InfoItemSearch->ListingCount == 0) return false;
        if (InfoItemSearch->SearchItemId != AddonItemSearchResult->AtkValues[0].Int) return false;

        SetMarkPriceValueAndReturn(GainLowestPriceFromInfoListings());
        return true;

        uint GainLowestPriceFromInfoListings()
        {
            var listings = InfoItemSearch->Listings;
            var filteredItems = new List<MarketBoardListing>();
            var currentMaxPrice = 0U;
            foreach (var item in listings)
            {
                if (PlayerRetainers.Contains(item.SellingRetainerContentId)) continue;
                if (ConfigSeparateNQAndHQ && IsCurrentItemHQ && !item.IsHqItem) continue;
                if (item.UnitPrice <= 0) continue;
                if (item.UnitPrice < currentMaxPrice) break;
                currentMaxPrice = item.UnitPrice;
                filteredItems.Add(item);
            }

            return filteredItems.MinBy(x => x.UnitPrice).UnitPrice;
        }

        void SetMarkPriceValueAndReturn(uint price)
        {
            CurrentItemSearchItemID = InfoItemSearch->SearchItemId;
            if ((int)price != 0)
            {
                if (!PriceCache.TryAdd((CurrentItemSearchItemID, IsCurrentItemHQ), (int)price))
                {
                    PriceCache[(CurrentItemSearchItemID, IsCurrentItemHQ)] = (int)price;
                }
            }
            AddonItemHistory->Close(true);
            AddonItemSearchResult->Close(true);

            TaskManager.EnqueueImmediate(() => FillLowestPrice((int)price));
        }
    }

    // 填入最低价格
    private bool? FillLowestPrice(int currentMarketLowestPrice)
    {
        if (InterruptByConflictKey()) return true;
        if (AddonItemSearchResult != null) return false;

        if (!TryGetAddonByName<AddonRetainerSell>("RetainerSell", out var addon) ||
            !HelpersOm.IsAddonAndNodesReady(&addon->AtkUnitBase)) return false;

        var ui = &addon->AtkUnitBase;
        var priceComponent = addon->AskingPrice;
        var handler = new ClickRetainerSellDR((nint)addon);

        var originalPrice = ui->AtkValues[5].Int;
        var modifiedPrice = currentMarketLowestPrice - ConfigPriceReduction;

        // 价格不变
        if (modifiedPrice == originalPrice)
        {
            OperateAndReturn(false);
            return true;
        }

        // 低于最低价
        if (modifiedPrice < ConfigLowestPrice)
        {
            var message = Service.Lang.GetSeString("AutoRetainerPriceAdjust-WarnMessageReachLowestPrice",
                                                   SeString.CreateItemLink(
                                                       CurrentItemSearchItemID,
                                                       IsCurrentItemHQ
                                                           ? ItemPayload.ItemKind.Hq
                                                           : ItemPayload.ItemKind.Normal), currentMarketLowestPrice,
                                                   originalPrice, ConfigLowestPrice);
            Service.Chat.Print(message);

            OperateAndReturn(false);
            return true;
        }

        // 超过可接受的降价值
        if (ConfigMaxPriceReduction != 0 && originalPrice - currentMarketLowestPrice > ConfigMaxPriceReduction)
        {
            var message = Service.Lang.GetSeString("AutoRetainerPriceAdjust-MaxPriceReductionMessage",
                                                   SeString.CreateItemLink(
                                                       CurrentItemSearchItemID,
                                                       IsCurrentItemHQ
                                                           ? ItemPayload.ItemKind.Hq
                                                           : ItemPayload.ItemKind.Normal), currentMarketLowestPrice,
                                                   originalPrice, ConfigMaxPriceReduction);
            Service.Chat.Print(message);

            OperateAndReturn(false);
            return true;
        }

        OperateAndReturn(true, modifiedPrice);
        return true;

        void OperateAndReturn(bool isConfirm, int price = 0)
        {
            if (isConfirm)
            {
                priceComponent->SetValue(price);
                handler.Confirm();
            }
            else handler.Decline();

            ui->Close(true);
            ResetCurrentItemStats();
        }
    }

    private static int GetSellListItemAmount()
    {
        var availableItems = 0;

        if (TryGetAddonByName<AtkUnitBase>("RetainerSellList", out var addon) && HelpersOm.IsAddonAndNodesReady(addon))
        {
            for (var i = 0; i < 20; i++)
                if (InventoryManager.Instance()->GetInventoryContainer(InventoryType.RetainerMarket)
                        ->GetInventorySlot(i)->ItemID !=
                    0)
                    availableItems++;
        }

        return availableItems;
    }

    public static List<(bool HQ, uint Price, int Amount)> ScanItemHistory(AtkUnitBase* addon)
    {
        var result = new List<(bool HQ, uint Price, int Amount)>();
        var list = addon->GetComponentListById(10);

        for (var i = 0; i < list->ListLength; i++)
        {
            var listing = list->ItemRendererList[i].AtkComponentListItemRenderer->AtkComponentButton.AtkComponentBase.UldManager.NodeList;
            var isHQ = listing[8]->IsVisible;
            if (!uint.TryParse(
                    SanitizeManager.Sanitize(listing[6]->GetAsAtkTextNode()->NodeText.ExtractText()).Replace(",", ""),
                    out var price)) continue;
            if (!int.TryParse(listing[5]->GetAsAtkTextNode()->NodeText.ExtractText(), out var amount)) continue;
            result.Add((isHQ, price, amount));
        }

        return result;
    }

    private static void ResetCurrentItemStats()
    {
        CurrentItemSearchItemID = 0;
        IsCurrentItemHQ = false;
    }

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnRetainerSellList);

        Service.AddonLifecycle.UnregisterListener(OnRetainerList);
        Service.AddonLifecycle.UnregisterListener(OnRetainerSell);

        base.Uninit();
    }

    [GeneratedRegex("[^0-9]")]
    private static partial Regex AutoRetainerPriceAdjustRegex();
}
