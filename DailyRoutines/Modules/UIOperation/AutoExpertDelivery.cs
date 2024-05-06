using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using ClickLib;
using ClickLib.Clicks;
using DailyRoutines.Infos;
using DailyRoutines.Infos.Clicks;
using DailyRoutines.Managers;
using DailyRoutines.Windows;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Interface.Colors;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoExpertDeliveryTitle", "AutoExpertDeliveryDescription", ModuleCategories.界面操作)]
public unsafe class AutoExpertDelivery : DailyModuleBase
{
    private static AtkUnitBase* GrandCompanySupplyList =>
        (AtkUnitBase*)Service.Gui.GetAddonByName("GrandCompanySupplyList");
    private static AtkUnitBase* GrandCompanySupplyReward =>
        (AtkUnitBase*)Service.Gui.GetAddonByName("GrandCompanySupplyReward");
    private static AtkUnitBase* SelectYesno =>
        (AtkUnitBase*)Service.Gui.GetAddonByName("SelectYesno");

    private static readonly List<InventoryType> ValidInventoryTypes =
    [
        InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3,
        InventoryType.Inventory4, InventoryType.ArmoryBody, InventoryType.ArmoryEar, InventoryType.ArmoryFeets,
        InventoryType.ArmoryHands, InventoryType.ArmoryHead, InventoryType.ArmoryLegs, InventoryType.ArmoryRings,
        InventoryType.ArmoryNeck, InventoryType.ArmoryWrist, InventoryType.ArmoryRings, InventoryType.ArmoryMainHand,
        InventoryType.ArmoryOffHand
    ];
    private static HashSet<uint> HQItems = [];

    private static bool SkipWhenHQ;
    private static DateTime? _LastUpdate;

    public override void Init()
    {
        AddConfig(nameof(SkipWhenHQ), SkipWhenHQ);
        SkipWhenHQ = GetConfig<bool>("SkipWhenHQ");

        _LastUpdate ??= DateTime.Now;

        Overlay ??= new Overlay(this);

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "GrandCompanySupplyList", OnAddonSupplyList);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "GrandCompanySupplyList", OnAddonSupplyList);
    }

    public override void OverlayUI()
    {
        var addon = (AtkUnitBase*)Service.Gui.GetAddonByName("GrandCompanySupplyList");
        if (addon == null) return;

        var pos = new Vector2(addon->GetX() - ImGui.GetWindowSize().X, addon->GetY() + 6);
        ImGui.SetWindowPos(pos);

        ImGui.TextColored(ImGuiColors.DalamudYellow, Service.Lang.GetText("AutoExpertDeliveryTitle"));

        ImGui.Separator();

        ImGui.BeginDisabled(DateTime.Now - _LastUpdate <= TimeSpan.FromSeconds(1));
        if (ImGui.Checkbox(Service.Lang.GetText("AutoExpertDelivery-SkipHQ"), ref SkipWhenHQ))
            UpdateConfig("SkipWhenHQ", SkipWhenHQ);

        if (ImGui.Button(Service.Lang.GetText("Start"))) Service.FrameworkManager.Register(OnUpdate);
        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("Stop"))) Service.FrameworkManager.Unregister(OnUpdate);
    }

    private static void OnUpdate(IFramework framework)
    {
        if (!EzThrottler.Throttle("AutoExpertDelivery", 100)) return;
        _LastUpdate = framework.LastUpdate;

        // 点击确认提交页面
        ConfirmRewardUI();

        // 检查是否即将超限
        CheckIfToReachCap();

        // 点击确认提交 HQ 物品
        ConfirmHQItemUI();

        // 点击列表物品
        ClickListUI();
    }

    private static void CheckIfToReachCap()
    {
        if (GrandCompanySupplyList == null || !IsAddonAndNodesReady(GrandCompanySupplyList)) return;

        var parts = Marshal.PtrToStringUTF8((nint)AtkStage.GetSingleton()->GetStringArrayData()[32]->StringArray[2])
                           .Split('/');
        var capAmount = int.Parse(parts[1].Replace(",", ""));

        var grandCompany = UIState.Instance()->PlayerState.GrandCompany;
        if ((GrandCompany)grandCompany == GrandCompany.None)
        {
            Service.FrameworkManager.Unregister(OnUpdate);
            GrandCompanySupplyReward->Close(true);
            SelectYesno->Close(true);
            return;
        }

        var companySeals = InventoryManager.Instance()->GetCompanySeals(grandCompany);

        var firstItemAmount = GrandCompanySupplyList->AtkValues[265].UInt;
        if (firstItemAmount + companySeals > capAmount)
        {
            Service.FrameworkManager.Unregister(OnUpdate);
            GrandCompanySupplyReward->Close(true);
            SelectYesno->Close(true);
        }
    }

    private static void ConfirmRewardUI()
    {
        if (GrandCompanySupplyReward == null || !IsAddonAndNodesReady(GrandCompanySupplyReward)) return;
        if (GrandCompanySupplyList != null && IsAddonAndNodesReady(GrandCompanySupplyList))
            GrandCompanySupplyList->Close(false);

        ClickGrandCompanySupplyReward.Using((nint)GrandCompanySupplyReward).Deliver();
    }

    private static void ConfirmHQItemUI()
    {
        if (!TryGetAddonByName<AtkUnitBase>("SelectYesno", out var addon) || !IsAddonAndNodesReady(addon)) return;
        Click.SendClick(SkipWhenHQ ? "select_no" : "select_yes");
    }

    private static void ClickListUI()
    {
        if (GrandCompanySupplyList == null || !IsAddonAndNodesReady(GrandCompanySupplyList)) return;

        var addon = (AddonGrandCompanySupplyList*)GrandCompanySupplyList;
        var handler = new ClickGrandCompanySupplyList();
        handler.ExpertDelivery();
        if (SkipWhenHQ)
        {
            HQItems = InventoryScanner(ValidInventoryTypes);

            var onlyHQLeft = true;
            for (var i = 0; i < addon->ExpertDeliveryList->ListLength; i++)
            {
                var itemID = addon->AtkUnitBase.AtkValues[i + 425].UInt;
                var isHQItem = HQItems.Contains(itemID);
                if (isHQItem) continue;

                handler.ItemEntry(i);
                onlyHQLeft = false;
                break;
            }

            if (onlyHQLeft)
                Service.FrameworkManager.Unregister(OnUpdate);
        }
        else
            handler.ItemEntry(0);
    }

    public static HashSet<uint> InventoryScanner(IEnumerable<InventoryType> inventories)
    {
        var inventoryManager = InventoryManager.Instance();

        var list = new HashSet<uint>();
        if (inventoryManager == null) return list;

        foreach (var inventory in inventories)
        {
            var container = inventoryManager->GetInventoryContainer(inventory);
            if (container == null) continue;

            for (var i = 0; i < container->Size; i++)
            {
                var slot = container->GetInventorySlot(i);
                if (slot == null) continue;

                var item = slot->ItemID;
                if (item == 0) continue;

                if (!slot->Flags.HasFlag(InventoryItem.ItemFlags.HQ)) continue;

                list.Add(item);
            }
        }

        return list;
    }

    private void OnAddonSupplyList(AddonEvent type, AddonArgs args)
    {
        Overlay.IsOpen = type switch
        {
            AddonEvent.PostSetup => true,
            AddonEvent.PreFinalize => false,
            _ => Overlay.IsOpen
        };
    }

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddonSupplyList);

        base.Uninit();
    }
}
