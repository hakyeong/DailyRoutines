using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using DailyRoutines.Clicks;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game;
using Dalamud.Game.AddonLifecycle;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Internal.Notifications;
using ECommons.Automation;
using ECommons.Throttlers;
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
    public bool WithUI => true;

    private static TaskManager? TaskManager;

    private static int ConfigPriceReduction;
    private static int ConfigLowestPrice;

    private static int CurrentMarketLowestPrice;
    private static uint CurrentItemSearchItemID;
    private static unsafe RetainerManager.RetainerList.Retainer* CurrentRetainer;


    public void Init()
    {
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 10000, ShowDebug = false };

        if (!Service.Config.ConfigExists(typeof(AutoRetainerPriceAdjust), "PriceReduction"))
            Service.Config.AddConfig(typeof(AutoRetainerPriceAdjust), "PriceReduction", 1);
        if (!Service.Config.ConfigExists(typeof(AutoRetainerPriceAdjust), "LowestAcceptablePrice"))
            Service.Config.AddConfig(typeof(AutoRetainerPriceAdjust), "LowestAcceptablePrice", 100);

        ConfigPriceReduction = Service.Config.GetConfig<int>(typeof(AutoRetainerPriceAdjust), "PriceReduction");
        ConfigLowestPrice = Service.Config.GetConfig<int>(typeof(AutoRetainerPriceAdjust), "LowestAcceptablePrice");

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "RetainerSellList", OnRetainerSellList);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "RetainerSell", OnRetainerSell);

        Service.Framework.Update += OnUpdate;

        Initialized = true;
    }

    public void UI()
    {
        ImGui.Text($"{Service.Lang.GetText("ConflictKey")}: {Service.Config.ConflictKey}");

        ImGui.SetNextItemWidth(210f);
        if (ImGui.InputInt(
                $"{Service.Lang.GetText("AutoRetainerPriceAdjust-SinglePriceReductionValue")}##AutoRetainerPriceAdjust-SinglePriceReductionValue",
                ref ConfigPriceReduction))
        {
            ConfigPriceReduction = Math.Max(1, ConfigPriceReduction);
            Service.Config.AddConfig(typeof(AutoRetainerPriceAdjust), "SinglePriceReductionValue", ConfigPriceReduction);
        }


        ImGui.SetNextItemWidth(210f);
        if (ImGui.InputInt(
                $"{Service.Lang.GetText("AutoRetainerPriceAdjust-LowestAcceptablePrice")}##AutoRetainerPriceAdjust-LowestAcceptablePrice",
                ref ConfigLowestPrice))
        {
            ConfigLowestPrice = Math.Max(1, ConfigLowestPrice);
            Service.Config.AddConfig(typeof(AutoRetainerPriceAdjust), "LowestAcceptablePrice", ConfigLowestPrice);
        }
    }

    private static void OnUpdate(Framework framework)
    {
        if (!TaskManager.IsBusy) return;

        if (Service.KeyState[Service.Config.ConflictKey])
        {
            TaskManager.Abort();
            P.PluginInterface.UiBuilder.AddNotification(Service.Lang.GetText("ConflictKey-InterruptMessage"), "Daily Routines", NotificationType.Success);
        }
    }

    private static void OnRetainerSell(AddonEvent eventType, AddonArgs addonInfo)
    {
        if (TaskManager.IsBusy) return;

        TaskManager.Abort();

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
            // 同一物品
            if (AgentItemSearch.Instance()->ResultItemID == CurrentItemSearchItemID)
            {
                addon->Close(true);
                return true;
            }

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

            var text = addon->UldManager.NodeList[5]->GetAsAtkComponentNode()->Component->UldManager.NodeList[1]->GetAsAtkComponentNode()->Component->UldManager.NodeList[10]->GetAsAtkTextNode()->NodeText.ToString();
            if (!int.TryParse(AutoRetainerPriceAdjustRegex().Replace(text, ""), out CurrentMarketLowestPrice)) return false;
            addon->Close(true);
            return true;
        }

        return false;
    }

    private static unsafe bool? FillLowestPrice()
    {
        if (TryGetAddonByName<AddonRetainerSell>("RetainerSell", out var addon) && HelpersOm.IsAddonAndNodesReady(&addon->AtkUnitBase))
        {
            var ui = &addon->AtkUnitBase;
            var priceComponent = addon->AskingPrice;
            var handler = new ClickRetainerSellDR((nint)addon);
            if (CurrentMarketLowestPrice < ConfigLowestPrice || CurrentMarketLowestPrice == 0 ||
                CurrentMarketLowestPrice - ConfigPriceReduction <= 1)
            {
                var message = Service.Lang.GetSeString("AutoRetainerPriceAdjust-WarnMessageReachLowestPrice", SeString.CreateItemLink(CurrentItemSearchItemID), CurrentMarketLowestPrice, ConfigLowestPrice);
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
        Service.Config.Save();

        Initialized = false;
    }

    [GeneratedRegex("[^0-9]")]
    private static partial Regex AutoRetainerPriceAdjustRegex();
}
