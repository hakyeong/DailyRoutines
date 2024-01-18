using System;
using ClickLib.Bases;
using DailyRoutines.Clicks;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.AddonLifecycle;
using Dalamud.Game.Text.SeStringHandling;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using static System.Text.RegularExpressions.Regex;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoRetainerPriceAdjustTitle", "AutoRetainerPriceAdjustDescription", ModuleCategories.Retainer)]
public class AutoRetainerPriceAdjust : IDailyModule
{
    public bool Initialized { get; set; }

    private static TaskManager? TaskManager;

    private static int ConfigPriceReduction;
    private static int ConfigLowestPrice;
    private static int CurrentMarketLowestPrice;
    private static unsafe RetainerManager.RetainerList.Retainer* CurrentRetainer;


    public void Init()
    {
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = false };

        Service.Config.AddConfig(typeof(AutoRetainerPriceAdjust), "PriceReduction", "1");
        Service.Config.AddConfig(typeof(AutoRetainerPriceAdjust), "LowestAcceptablePrice", "1");
        ConfigPriceReduction = Service.Config.GetConfig<int>(typeof(AutoRetainerPriceAdjust), "PriceReduction");
        ConfigLowestPrice = Service.Config.GetConfig<int>(typeof(AutoRetainerPriceAdjust), "LowestAcceptablePrice");

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "RetainerSellList", OnRetainerSellList);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "RetainerSell", OnRetainerSell);

        Initialized = true;
    }

    public void UI()
    {
        ImGui.SetNextItemWidth(210f);
        if (ImGui.InputInt(
                $"{Service.Lang.GetText("AutoRetainerPriceAdjust-SinglePriceReductionValue")}##SinglePriceReductionValue",
                ref ConfigPriceReduction))
        {
            ConfigPriceReduction = Math.Max(1, ConfigPriceReduction);
            Service.Config.UpdateConfig(typeof(AutoRetainerPriceAdjust), "SinglePriceReductionValue",
                                        ConfigPriceReduction.ToString());
        }


        ImGui.SetNextItemWidth(210f);
        if (ImGui.InputInt(
                $"{Service.Lang.GetText("AutoRetainerPriceAdjust-LowestAcceptablePrice")}##LowestAcceptablePrice",
                ref ConfigLowestPrice))
        {
            ConfigLowestPrice = Math.Max(1, ConfigLowestPrice);
            Service.Config.UpdateConfig(typeof(AutoRetainerPriceAdjust), "LowestAcceptablePrice",
                                        ConfigLowestPrice.ToString());
        }
    }

    private static void OnRetainerSell(AddonEvent eventType, AddonArgs addonInfo)
    {
        if (TaskManager.IsBusy) return;

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
        TaskManager.DelayNext(1000);
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
        TaskManager.DelayNext(1000);
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
            return handler.ClickItem(index);
        }

        return false;
    }

    private static unsafe bool? ClickAdjustPrice()
    {
        if (TryGetAddonByName<AtkUnitBase>("ContextMenu", out var addon) && HelpersOm.IsAddonAndNodesReady(addon))
        {
            var handler = new ClickRetainerSellListContextMenuDR((nint)addon);
            return handler.AdjustPrice();
        }

        return false;
    }

    private static unsafe bool? ClickComparePrice()
    {
        if (TryGetAddonByName<AtkUnitBase>("RetainerSell", out var addon) && HelpersOm.IsAddonAndNodesReady(addon))
        {
            var handler = new ClickRetainerSellDR((nint)addon);
            return handler.ComparePrice();
        }

        return false;
    }

    private static unsafe bool? GetLowestPrice()
    {
        if (TryGetAddonByName<AtkUnitBase>("ItemSearchResult", out var addon) && HelpersOm.IsAddonAndNodesReady(addon))
        {
            var text = addon->UldManager.NodeList[5]->GetAsAtkComponentNode()->Component->UldManager.NodeList[1]->
                GetAsAtkComponentNode()->Component->UldManager.NodeList[10]->GetAsAtkTextNode()->NodeText.ToString();
            if (!int.TryParse(Replace(text, "[^0-9]", ""), out CurrentMarketLowestPrice)) return false;
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
            var itemName = addon->ItemName->NodeText.ExtractText();
            Service.Log.Debug(CurrentMarketLowestPrice.ToString());
            if (CurrentMarketLowestPrice < ConfigLowestPrice || CurrentMarketLowestPrice == 0 || CurrentMarketLowestPrice - ConfigPriceReduction <= 1)
            {
                var message = Service.Lang.GetSeString("AutoRetainerPriceAdjust-WarnMessageReachLowestPrice",
                                                       SeString.CreateItemLink(Service.ExcelData.ItemNames[itemName]),
                                                       CurrentMarketLowestPrice, ConfigLowestPrice);
                Service.Chat.Print(message);

                handler.Cancel();
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
        Service.AddonLifecycle.UnregisterListener(OnRetainerSellList);
        Service.AddonLifecycle.UnregisterListener(OnRetainerSell);
        TaskManager?.Abort();
        Service.Config.Save();

        Initialized = false;
    }
}
