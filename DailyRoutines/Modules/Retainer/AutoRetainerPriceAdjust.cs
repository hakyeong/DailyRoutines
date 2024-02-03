using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using DailyRoutines.Clicks;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game;
using Dalamud.Game.AddonLifecycle;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Internal.Notifications;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoRetainerPriceAdjustTitle", "AutoRetainerPriceAdjustDescription", ModuleCategories.Retainer)]
public partial class AutoRetainerPriceAdjust : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithConfigUI => true;

    private static TaskManager? TaskManager;

    private static int ConfigPriceReduction;
    private static int ConfigLowestPrice;
    private static int ConfigMaxPriceReduction;
    private static bool ConfigSeparateNQAndHQ;

    private static int CurrentItemPrice;
    private static int CurrentMarketLowestPrice;
    private static uint CurrentItemSearchItemID;
    private static bool IsCurrentItemHQ;
    private static unsafe RetainerManager.RetainerList.Retainer* CurrentRetainer;

    public void Init()
    {
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 10000, ShowDebug = false };

        Service.Config.AddConfig(this, "PriceReduction", 1);
        Service.Config.AddConfig(this, "LowestAcceptablePrice", 100);
        Service.Config.AddConfig(this, "SeparateNQAndHQ", false);
        Service.Config.AddConfig(this, "MaxPriceReduction", 0);

        ConfigPriceReduction = Service.Config.GetConfig<int>(this, "PriceReduction");
        ConfigLowestPrice = Service.Config.GetConfig<int>(this, "LowestAcceptablePrice");
        ConfigSeparateNQAndHQ = Service.Config.GetConfig<bool>(this, "SeparateNQAndHQ");
        ConfigMaxPriceReduction = Service.Config.GetConfig<int>(this, "MaxPriceReduction");

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "RetainerSellList", OnRetainerSellList);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "RetainerSell", OnRetainerSell);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "RetainerSell", OnRetainerSell);

        Service.Framework.Update += OnUpdate;

        Initialized = true;
    }

    public void ConfigUI()
    {
        ImGui.Text($"{Service.Lang.GetText("ConflictKey")}: {Service.Config.ConflictKey}");

        ImGui.SetNextItemWidth(210f);
        if (ImGui.InputInt(
                $"{Service.Lang.GetText("AutoRetainerPriceAdjust-SinglePriceReductionValue")}##AutoRetainerPriceAdjust-SinglePriceReductionValue",
                ref ConfigPriceReduction, 100))
        {
            ConfigPriceReduction = Math.Max(1, ConfigPriceReduction);
            Service.Config.UpdateConfig(this, "SinglePriceReductionValue", ConfigPriceReduction);
        }


        ImGui.SetNextItemWidth(210f);
        if (ImGui.InputInt(
                $"{Service.Lang.GetText("AutoRetainerPriceAdjust-LowestAcceptablePrice")}##AutoRetainerPriceAdjust-LowestAcceptablePrice",
                ref ConfigLowestPrice, 100))
        {
            ConfigLowestPrice = Math.Max(1, ConfigLowestPrice);
            Service.Config.UpdateConfig(this, "LowestAcceptablePrice", ConfigLowestPrice);
        }

        ImGui.SetNextItemWidth(210f);
        if (ImGui.InputInt(
                $"{Service.Lang.GetText("AutoRetainerPriceAdjust-MaxPriceReduction")}##AutoRetainerPriceAdjust-MaxPriceReduction",
                ref ConfigMaxPriceReduction, 100))
        {
            ConfigMaxPriceReduction = Math.Max(0, ConfigMaxPriceReduction);
            Service.Config.UpdateConfig(this, "MaxPriceReduction", ConfigMaxPriceReduction);
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Service.Lang.GetText("AutoRetainerPriceAdjust-MaxPriceReductionInputHelp"));

        if (ImGui.Checkbox(
                $"{Service.Lang.GetText("AutoRetainerPriceAdjust-SeparateNQAndHQ")}##AutoRetainerPriceAdjust-SeparateNQAndHQ",
                ref ConfigSeparateNQAndHQ))
            Service.Config.UpdateConfig(this, "SeparateNQAndHQ", ConfigSeparateNQAndHQ);

        ImGuiOm.HelpMarker(Service.Lang.GetText("AutoRetainerPriceAdjust-SeparateNQAndHQHelp"));
    }

    public void OverlayUI() { }

    private static void OnUpdate(Framework framework)
    {
        if (!TaskManager.IsBusy) return;

        if (Service.KeyState[Service.Config.ConflictKey])
        {
            TaskManager.Abort();
            P.PluginInterface.UiBuilder.AddNotification(Service.Lang.GetText("ConflictKey-InterruptMessage"),
                                                        "Daily Routines", NotificationType.Success);
        }
    }

    private static void OnRetainerSell(AddonEvent eventType, AddonArgs addonInfo)
    {
        switch (eventType)
        {
            case AddonEvent.PostSetup:
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
                if (TaskManager.NumQueuedTasks <= 1)
                    TaskManager.Abort();
                break;
        }
    }

    private static unsafe void OnRetainerSellList(AddonEvent type, AddonArgs args)
    {
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

    private static void EnqueueSingleItem(int index)
    {
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

    private static unsafe void GetSellListItems(out uint availableItems)
    {
        availableItems = 0;
        if (TryGetAddonByName<AtkUnitBase>("RetainerSellList", out var addon) && HelpersOm.IsAddonAndNodesReady(addon))
        {
            for (var i = 0; i < 20; i++)
            {
                var slot =
                    InventoryManager.Instance()->GetInventoryContainer(InventoryType.RetainerMarket)->GetInventorySlot(
                        i);
                if (slot->ItemID != 0) availableItems++;
            }
        }
    }

    private static unsafe bool? ClickSellingItem(int index)
    {
        if (TryGetAddonByName<AtkUnitBase>("RetainerSellList", out var addon) && HelpersOm.IsAddonAndNodesReady(addon))
        {
            var handler = new ClickRetainerSellListDR((nint)addon);
            handler.ItemEntry(index);
            return true;
        }

        return false;
    }

    private static unsafe bool? ClickAdjustPrice()
    {
        if (TryGetAddonByName<AtkUnitBase>("ContextMenu", out var addon) && HelpersOm.IsAddonAndNodesReady(addon))
        {
            var handler = new ClickRetainerSellListContextMenuDR((nint)addon);
            handler.AdjustPrice();

            return true;
        }

        return false;
    }

    private static unsafe bool? ClickComparePrice()
    {
        if (TryGetAddonByName<AtkUnitBase>("RetainerSell", out var addon) && HelpersOm.IsAddonAndNodesReady(addon))
        {
            CurrentItemPrice = addon->AtkValues[5].Int;
            IsCurrentItemHQ = Marshal.PtrToStringUTF8((nint)addon->AtkValues[1].String).Contains(''); // 是游戏里的 HQ 符号

            var handler = new ClickRetainerSellDR((nint)addon);
            handler.ComparePrice();

            return true;
        }

        return false;
    }

    private static unsafe bool? GetLowestPrice()
    {
        if (TryGetAddonByName<AtkUnitBase>("ItemSearchResult", out var addon) && HelpersOm.IsAddonAndNodesReady(addon))
        {
            CurrentItemSearchItemID = AgentItemSearch.Instance()->ResultItemID;
            var searchResult = addon->GetTextNodeById(29)->NodeText.ExtractText();
            if (string.IsNullOrEmpty(searchResult)) return false; // 请稍后

            // 搜索结果 0
            if (int.Parse(AutoRetainerPriceAdjustRegex().Replace(searchResult, "")) == 0)
            {
                CurrentMarketLowestPrice = 0;
                addon->Close(true);
                return true;
            }

            // 区分 HQ 和 NQ
            if (ConfigSeparateNQAndHQ && IsCurrentItemHQ)
            {
                var foundHQItem = false;
                for (var i = 1; i <= 12 && !foundHQItem; i++)
                {
                    var listing =
                        addon->UldManager.NodeList[5]->GetAsAtkComponentNode()->Component->UldManager.NodeList[i]
                            ->GetAsAtkComponentNode()->Component->UldManager.NodeList;
                    if (listing[13]->GetAsAtkImageNode()->AtkResNode.IsVisible)
                    {
                        var priceText = listing[10]->GetAsAtkTextNode()->NodeText.ExtractText();
                        if (int.TryParse(AutoRetainerPriceAdjustRegex().Replace(priceText, ""),
                                         out CurrentMarketLowestPrice))
                            foundHQItem = true;
                    }
                }

                if (!foundHQItem)
                {
                    var priceText = addon->UldManager.NodeList[5]->GetAsAtkComponentNode()->Component->UldManager
                                .NodeList[1]
                            ->GetAsAtkComponentNode()->Component->UldManager.NodeList[10]->GetAsAtkTextNode()->NodeText
                        .ExtractText();
                    if (!int.TryParse(AutoRetainerPriceAdjustRegex().Replace(priceText, ""),
                                      out CurrentMarketLowestPrice)) return false;
                }
            }
            else
            {
                var priceText =
                    addon->UldManager.NodeList[5]->GetAsAtkComponentNode()->Component->UldManager.NodeList[1]
                            ->GetAsAtkComponentNode()->Component->UldManager.NodeList[10]->GetAsAtkTextNode()->NodeText
                        .ExtractText();
                if (!int.TryParse(AutoRetainerPriceAdjustRegex().Replace(priceText, ""),
                                  out CurrentMarketLowestPrice)) return false;
            }

            addon->Close(true);
            return true;
        }

        return false;
    }

    private static unsafe bool? FillLowestPrice()
    {
        if (TryGetAddonByName<AddonRetainerSell>("RetainerSell", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(&addon->AtkUnitBase))
        {
            var ui = &addon->AtkUnitBase;
            var priceComponent = addon->AskingPrice;
            var handler = new ClickRetainerSellDR((nint)addon);

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

                return true;
            }

            // 超过可接受的降价值
            if (ConfigMaxPriceReduction != 0 && CurrentItemPrice - CurrentMarketLowestPrice > ConfigLowestPrice)
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

                return true;
            }

            priceComponent->SetValue(CurrentMarketLowestPrice - ConfigPriceReduction);
            handler.Confirm();
            ui->Close(true);

            return true;
        }

        return false;
    }

    public void Uninit()
    {
        Service.Framework.Update -= OnUpdate;
        Service.AddonLifecycle.UnregisterListener(OnRetainerSellList);
        Service.AddonLifecycle.UnregisterListener(OnRetainerSell);
        TaskManager?.Abort();

        Initialized = false;
    }

    [GeneratedRegex("[^0-9]")]
    private static partial Regex AutoRetainerPriceAdjustRegex();
}
