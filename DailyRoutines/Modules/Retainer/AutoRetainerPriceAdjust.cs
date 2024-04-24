using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using ClickLib;
using ClickLib.Clicks;
using DailyRoutines.Helpers;
using DailyRoutines.Infos;
using DailyRoutines.Infos.Clicks;
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
using Lumina.Excel.GeneratedSheets;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoRetainerPriceAdjustTitle", "AutoRetainerPriceAdjustDescription", ModuleCategories.Retainer)]
public unsafe partial class AutoRetainerPriceAdjust : DailyModuleBase
{
    private static AtkUnitBase* AddonRetainerSellList => (AtkUnitBase*)Service.Gui.GetAddonByName("RetainerSellList");
    private static AtkUnitBase* AddonRetainerList => (AtkUnitBase*)Service.Gui.GetAddonByName("RetainerList");
    private static AtkUnitBase* AddonItemSearchResult => (AtkUnitBase*)Service.Gui.GetAddonByName("ItemSearchResult");
    private static AtkUnitBase* AddonItemHistory => (AtkUnitBase*)Service.Gui.GetAddonByName("ItemHistory");
    private static InfoProxyItemSearch* InfoItemSearch => (InfoProxyItemSearch*)InfoModule.Instance()->GetInfoProxyById(InfoProxyId.ItemSearch);

    private class Config : ModuleConfiguration
    {
        public int PriceReduction = 1;
        public int LowestPrice = 100;
        public int MaxPriceReduction;
        public bool SeparateNQAndHQ;
        public bool ProhibitLowerThanSellPrice = true;
        public bool AdjustToLowestPriceWhenLower = true;
    }

    private static Config ModuleConfig = null!;
    
    private static Dictionary<(uint Id, bool HQ), int> PriceCache = [];
    private static bool IsCurrentItemHQ;
    private static uint CurrentItemSearchItemID;
    private static readonly HashSet<ulong> PlayerRetainers = [];

    private static Dictionary<uint, uint>? ItemsSellPrice;

    public override void Init()
    {
        ModuleConfig = LoadConfig<Config>() ?? new();

        ItemsSellPrice ??= LuminaCache.Get<Item>()
                                  .Where(x => !string.IsNullOrEmpty(x.Name.RawString) && x.PriceLow != 0)
                                  .ToDictionary(x => x.RowId, x => x.PriceLow);

        // 出售品列表
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "RetainerSellList", OnRetainerSellList);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "RetainerSellList", OnRetainerSellList);
        // 雇员列表
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "RetainerList", OnRetainerList);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "RetainerList", OnRetainerList);

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "RetainerSell", OnRetainerSell);

        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 60000, ShowDebug = false };
        Overlay ??= new Overlay(this);
    }

    public override void ConfigUI()
    {
        ConflictKeyText();

        ImGui.SetNextItemWidth(150f * ImGuiHelpers.GlobalScale);
        ImGui.InputInt($"{Service.Lang.GetText("AutoRetainerPriceAdjust-SinglePriceReductionValue")}##AutoRetainerPriceAdjust-SinglePriceReductionValue", ref ModuleConfig.PriceReduction, 100);
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            ModuleConfig.PriceReduction = Math.Max(0, ModuleConfig.PriceReduction);
            SaveConfig(ModuleConfig);
        }

        ImGui.SetNextItemWidth(150f * ImGuiHelpers.GlobalScale);
        ImGui.InputInt($"{Service.Lang.GetText("AutoRetainerPriceAdjust-LowestAcceptablePrice")}##AutoRetainerPriceAdjust-LowestAcceptablePrice", ref ModuleConfig.LowestPrice, 100);
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            ModuleConfig.LowestPrice = Math.Max(1, ModuleConfig.LowestPrice);
            SaveConfig(ModuleConfig);
        }

        ImGui.SetNextItemWidth(150f * ImGuiHelpers.GlobalScale);
        ImGui.InputInt($"{Service.Lang.GetText("AutoRetainerPriceAdjust-MaxPriceReduction")}##AutoRetainerPriceAdjust-MaxPriceReduction", ref ModuleConfig.MaxPriceReduction, 100);
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            ModuleConfig.MaxPriceReduction = Math.Max(0, ModuleConfig.MaxPriceReduction);
            SaveConfig(ModuleConfig);
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Service.Lang.GetText("AutoRetainerPriceAdjust-MaxPriceReductionInputHelp"));

        if (ImGui.Checkbox($"{Service.Lang.GetText("AutoRetainerPriceAdjust-SeparateNQAndHQ")}", ref ModuleConfig.SeparateNQAndHQ))
            SaveConfig(ModuleConfig);
        ImGuiOm.HelpMarker(Service.Lang.GetText("AutoRetainerPriceAdjust-SeparateNQAndHQHelp"));

        if (ImGui.Checkbox(Service.Lang.GetText("AutoRetainerPriceAdjust-ProhibitLowerThanSellPrice"), ref ModuleConfig.ProhibitLowerThanSellPrice))
            SaveConfig(ModuleConfig);

        if (ImGui.Checkbox(Service.Lang.GetText("AutoRetainerPriceAdjust-AdjustToLowestPriceWhenLower"), ref ModuleConfig.AdjustToLowestPriceWhenLower))
            SaveConfig(ModuleConfig);
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

        if (ImGui.CollapsingHeader(Service.Lang.GetText("ModuleConfig")))
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
        }
    }

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
            TaskManager.DelayNext(500);
            TaskManager.Enqueue(() =>
            {
                if (AddonRetainerSellList == null) return false;

                for (var i1 = 0; i1 < marketItemCount; i1++)
                {
                    var index1 = i1;
                    TaskManager.Insert(() => ClickSellingItem(index1));
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
            AgentHelper.SendEvent(AgentId.Retainer, 3, 0, index, 1);

            TaskManager.EnqueueImmediate(ClickAdjustPrice);
            return true;
        }

        return false;
    }

    // 点击右键菜单中的 修改价格
    private bool? ClickAdjustPrice()
    {
        if (InterruptByConflictKey()) return true;
        if (!ClickHelper.ContextMenu("修改价格")) return false;

        TaskManager.EnqueueImmediate(ClickComparePrice);
        return true;
    }

    // 点击比价按钮
    private bool? ClickComparePrice()
    {
        if (InterruptByConflictKey()) return true;

        if (TryGetAddonByName<AtkUnitBase>("RetainerSell", out var addon) && IsAddonAndNodesReady(addon))
        {
            IsCurrentItemHQ = Marshal.PtrToStringUTF8((nint)addon->AtkValues[1].String).Contains(''); // HQ 符号

            ClickRetainerSell.Using((nint)addon).ComparePrice();

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
        if (AddonItemSearchResult == null || !IsAddonAndNodesReady(AddonItemSearchResult)) return false;
        if (InfoItemSearch->SearchItemId != 0 && PriceCache.Count != 0)
        {
            if (PriceCache.TryGetValue((InfoItemSearch->SearchItemId, IsCurrentItemHQ), out var cachePrice) && cachePrice != 0)
            {
                AddonItemSearchResult->Close(true);
                TaskManager.EnqueueImmediate(() => FillLowestPrice(cachePrice));
                return true;
            }
        }

        if (AddonItemHistory == null) AddonHelper.Callback(AddonItemSearchResult, true, 0);
        if (!IsAddonAndNodesReady(AddonItemHistory)) return false;

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
                if (ModuleConfig.SeparateNQAndHQ && IsCurrentItemHQ)
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
                if (ModuleConfig.SeparateNQAndHQ && IsCurrentItemHQ && !item.IsHqItem) continue;
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

        if (!TryGetAddonByName<AddonRetainerSell>("RetainerSell", out var addon) || !IsAddonAndNodesReady(&addon->AtkUnitBase)) return false;

        var ui = &addon->AtkUnitBase;
        var priceComponent = addon->AskingPrice;

        var originalPrice = ui->AtkValues[5].Int;
        var modifiedPrice = currentMarketLowestPrice - ModuleConfig.PriceReduction;

        // 价格不变
        if (modifiedPrice == originalPrice)
        {
            OperateAndReturn(false);
            return true;
        }

        // 超过可接受的降价值
        if (ModuleConfig.MaxPriceReduction != 0 && originalPrice - currentMarketLowestPrice > ModuleConfig.MaxPriceReduction)
        {
            SendSkipPriceAdjustMessage(currentMarketLowestPrice, originalPrice,
                                       Service.Lang.GetText("AutoRetainerPriceAdjust-MaxPriceReduction"),
                                       ModuleConfig.MaxPriceReduction);

            OperateAndReturn(false);
            return true;
        }

        // 低于收购价格
        if (ModuleConfig.ProhibitLowerThanSellPrice && ItemsSellPrice.TryGetValue(CurrentItemSearchItemID, out var npcSellPrice) && modifiedPrice < npcSellPrice)
        {
            SendSkipPriceAdjustMessage(currentMarketLowestPrice, originalPrice,
                                       Service.Lang.GetText("AutoRetainerPriceAdjust-LowestAcceptablePrice"),
                                       (int)npcSellPrice);

            OperateAndReturn(false);
            return true;
        }

        // 低于最低价时改成最低价
        if (ModuleConfig.AdjustToLowestPriceWhenLower && modifiedPrice < ModuleConfig.LowestPrice)
        {
            OperateAndReturn(true, ModuleConfig.LowestPrice);
            return true;
        }

        // 低于最低价
        if (modifiedPrice < ModuleConfig.LowestPrice)
        {
            SendSkipPriceAdjustMessage(currentMarketLowestPrice, originalPrice,
                                       Service.Lang.GetText("AutoRetainerPriceAdjust-LowestAcceptablePrice"),
                                       ModuleConfig.LowestPrice);

            OperateAndReturn(false);
            return true;
        }

        OperateAndReturn(true, modifiedPrice);
        return true;

        void OperateAndReturn(bool isConfirm, int price = 0)
        {
            var handler = new ClickRetainerSell();
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

    private static void SendSkipPriceAdjustMessage(int currentMarketPrice, int originalPrice, string reason, int threshold)
    {
        var ssb = new SeStringBuilder();
        // 前缀
        var prefix = DRPrefix().Append(" ");
        // 物品链接
        var itemLink = SeString.CreateItemLink(CurrentItemSearchItemID, IsCurrentItemHQ ? ItemPayload.ItemKind.Hq : ItemPayload.ItemKind.Normal);
        // 雇员
        var retainerName = new SeStringBuilder()
                           .AddUiForeground(Marshal.PtrToStringUTF8((nint)RetainerManager.Instance()->GetActiveRetainer()->Name), 62)
                           .Build();
        // 拒绝理由
        var rReason = new SeStringBuilder().AddUiForeground(reason, 43).Build();
        // 消息正文
        var message = Service.Lang.GetSeString("AutoRetainerPriceAdjust-SkipAdjustMessage", itemLink, retainerName,
                                               currentMarketPrice, originalPrice, rReason, threshold);
        
        Service.Chat.Print(ssb.Append(prefix).Append(message).Build());
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
                    SanitizeHelper.Sanitize(listing[6]->GetAsAtkTextNode()->NodeText.ExtractText()).Replace(",", ""),
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
