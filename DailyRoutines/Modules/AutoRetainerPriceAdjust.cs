using System;
using ClickLib.Bases;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.AddonLifecycle;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using static System.Text.RegularExpressions.Regex;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoRetainerPriceAdjustTitle", "AutoRetainerPriceAdjustDescription", ModuleCategories.General)]
public class AutoRetainerPriceAdjust : IDailyModule
{
    public bool Initialized { get; set; }

    private static int ConfigPriceReduction;
    private static int ConfigLowestPrice;
    private static int CurrentMarketLowestPrice;

    public void Init()
    {
        Service.Config.AddConfig(typeof(AutoRetainerPriceAdjust), "PriceReduction", "1");
        Service.Config.AddConfig(typeof(AutoRetainerPriceAdjust), "LowestAcceptablePrice", "1");
        ConfigPriceReduction = Service.Config.GetConfig<int>(typeof(AutoRetainerPriceAdjust), "PriceReduction");
        ConfigLowestPrice = Service.Config.GetConfig<int>(typeof(AutoRetainerPriceAdjust), "LowestAcceptablePrice");
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "RetainerSellList", OnRetainerSellList);

        Initialized = true;
    }

    public void UI()
    {
        ImGui.AlignTextToFramePadding();
        ImGui.Text($"{Service.Lang.GetText("AutoRetainerPriceAdjust-SinglePriceReductionValue")}:");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(240f);
        if (ImGui.InputInt("##SinglePriceReductionValue", ref ConfigPriceReduction))
        {
            ConfigPriceReduction = Math.Max(1, ConfigPriceReduction);
            Service.Config.UpdateConfig(typeof(AutoRetainerPriceAdjust), "SinglePriceReductionValue",
                                        ConfigPriceReduction.ToString());
        }

        ImGui.AlignTextToFramePadding();
        ImGui.Text($"{Service.Lang.GetText("AutoRetainerPriceAdjust-LowestAcceptablePrice")}:");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(240f);
        if (ImGui.InputInt("##LowestAcceptablePrice", ref ConfigLowestPrice))
        {
            ConfigLowestPrice = Math.Max(1, ConfigLowestPrice);
            Service.Config.UpdateConfig(typeof(AutoRetainerPriceAdjust), "LowestAcceptablePrice",
                                        ConfigLowestPrice.ToString());
        }
    }

    private static void OnRetainerSellList(AddonEvent type, AddonArgs args)
    {
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
        P.TaskManager.Enqueue(() => ClickSellingItem(index));
        P.TaskManager.DelayNext(100);
        // 点击修改价格
        P.TaskManager.Enqueue(ClickAdjustPrice);
        P.TaskManager.DelayNext(100);
        // 点击比价
        P.TaskManager.Enqueue(ClickComparePrice);
        P.TaskManager.DelayNext(500);
        P.TaskManager.AbortOnTimeout = false;
        // 获取当前最低价，并退出
        P.TaskManager.Enqueue(GetLowestPrice);
        P.TaskManager.AbortOnTimeout = true;
        P.TaskManager.DelayNext(100);
        // 填写最低价
        P.TaskManager.Enqueue(FillLowestPrice);
        P.TaskManager.DelayNext(1000);
    }

    private static unsafe bool? GetSellListItems(out uint availableItems)
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

            Service.Log.Debug($"{availableItems}");

            return true;
        }

        return false;
    }

    private static unsafe bool? ClickSellingItem(int index)
    {
        if (TryGetAddonByName<AtkUnitBase>("RetainerSellList", out var addon) && HelpersOm.IsAddonAndNodesReady(addon))
        {
            var handler = new ClickRetainerSellList((nint)addon);
            return handler.ClickItem(index);
        }

        return false;
    }

    private static unsafe bool? ClickAdjustPrice()
    {
        if (TryGetAddonByName<AtkUnitBase>("ContextMenu", out var addon) && HelpersOm.IsAddonAndNodesReady(addon))
        {
            var handler = new ClickRetainerSellListContextMenu((nint)addon);
            return handler.AdjustPrice();
        }

        return false;
    }

    private static unsafe bool? ClickComparePrice()
    {
        if (TryGetAddonByName<AtkUnitBase>("RetainerSell", out var addon) && HelpersOm.IsAddonAndNodesReady(addon))
        {
            var handler = new ClickRetainerSell((nint)addon);
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
            addon->Close(false);
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
            Service.Log.Debug(CurrentMarketLowestPrice.ToString());
            if (CurrentMarketLowestPrice < ConfigLowestPrice)
            {
                var itemName = addon->ItemName->NodeText.ExtractText();
                var message = Service.Lang.GetSeString("AutoRetainerPriceAdjust-WarnMessageReachLowestPrice", SeString.CreateItemLink(Service.ExcelData.ItemNames[itemName]), CurrentMarketLowestPrice, ConfigLowestPrice);
                Service.Chat.PrintError(message);
            }
            if (CurrentMarketLowestPrice > ConfigLowestPrice && CurrentMarketLowestPrice != 0 && CurrentMarketLowestPrice - ConfigPriceReduction > 1)
                priceComponent->SetValue(CurrentMarketLowestPrice - ConfigPriceReduction);

            var handler = new ClickRetainerSell((nint)addon);
            handler.Confirm();
            ui->Close(false);
            return true;
        }

        return false;
    }

    public void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnRetainerSellList);
        P.TaskManager.Abort();
        Service.Config.Save();

        Initialized = false;
    }
}

public class ClickRetainerSellList(nint addon = default) : ClickBase<ClickRetainerSellList>("RetainerSellList", addon)
{
    public bool ClickItem(int index)
    {
        if (index is > 19 or < -1) return false;

        FireCallback(0, index, 1);

        return true;
    }
}

public class ClickRetainerSell(nint addon = default)
    : ClickBase<ClickRetainerSell, AddonRetainerSell>("RetainerSell", addon)
{
    public bool ComparePrice()
    {
        FireCallback(4);
        return true;
    }

    public bool Confirm()
    {
        FireCallback(0);
        return true;
    }

    public bool Cancel()
    {
        FireCallback(1);
        return true;
    }
}

public class ClickRetainerSellListContextMenu(nint addon = default)
    : ClickBase<ClickRetainerSellListContextMenu>("ContextMenu", addon)
{
    public bool AdjustPrice()
    {
        Click(0);
        return true;
    }

    public bool ReturnToRetainer()
    {
        Click(1);
        return true;
    }

    public bool ReturnToInventory()
    {
        Click(2);
        return true;
    }

    public bool Exit()
    {
        Click(-1);
        return true;
    }

    public bool Click(int index)
    {
        FireCallback(0, index, 0, 0, 0);
        return true;
    }
}
