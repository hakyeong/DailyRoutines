using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using ClickLib;
using ClickLib.Clicks;
using DailyRoutines.Clicks;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using DailyRoutines.Windows;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Interface.Colors;
using ECommons.Automation;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoExpertDeliveryTitle", "AutoExpertDeliveryDescription", ModuleCategories.InterfaceExpand)]
public unsafe class AutoExpertDelivery : DailyModuleBase
{
    private delegate byte AtkUnitBaseCloseDelegate(AtkUnitBase* unitBase, byte a2);
    private static AtkUnitBaseCloseDelegate? AtkUnitBaseClose;

    private static AtkUnitBase* AddonGrandCompanySupplyList => (AtkUnitBase*)Service.Gui.GetAddonByName("GrandCompanySupplyList");

    private static HashSet<uint> HQItems = [];

    private static bool ConfigSkipWhenHQ;

    private static readonly List<InventoryType> ValidInventoryTypes =
    [
        InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3,
        InventoryType.Inventory4, InventoryType.ArmoryBody, InventoryType.ArmoryEar, InventoryType.ArmoryFeets,
        InventoryType.ArmoryHands, InventoryType.ArmoryHead, InventoryType.ArmoryLegs, InventoryType.ArmoryRings,
        InventoryType.ArmoryNeck, InventoryType.ArmoryWrist, InventoryType.ArmoryRings, InventoryType.ArmoryMainHand,
        InventoryType.ArmoryOffHand
    ];

    public override void Init()
    {
        AddConfig(this, "SkipWhenHQ", ConfigSkipWhenHQ);
        ConfigSkipWhenHQ = GetConfig<bool>(this, "SkipWhenHQ");

        AtkUnitBaseClose ??= Marshal.GetDelegateForFunctionPointer<AtkUnitBaseCloseDelegate>(Service.SigScanner.ScanText("40 53 48 83 EC 50 81 A1"));

        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 10000, ShowDebug = true };
        Overlay ??= new Overlay(this);

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, "SelectYesno", AlwaysYes);
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

        ImGui.BeginDisabled(TaskManager.IsBusy);
        if (ImGui.Checkbox(Service.Lang.GetText("AutoExpertDelivery-SkipHQ"), ref ConfigSkipWhenHQ))
            UpdateConfig(this, "SkipWhenHQ", ConfigSkipWhenHQ);

        if (ImGui.Button(Service.Lang.GetText("Start"))) EnqueueAllItems();
        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("Stop"))) TaskManager.Abort();
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

    private void AlwaysYes(AddonEvent type, AddonArgs args)
    {
        if (!TaskManager.IsBusy) return;
        Click.SendClick("select_yes");
    }

    private void EnqueueAllItems()
    {
        if (AddonGrandCompanySupplyList == null) return;

        var handler = new ClickGrandCompanySupplyListDR();
        handler.ExpertDelivery();

        var listCount = ((AddonGrandCompanySupplyList*)AddonGrandCompanySupplyList)->ExpertDeliveryList->ListLength;
        if (listCount == 0) return;

        if (ConfigSkipWhenHQ)
        {
            HQItems.Clear();
            HQItems = InventoryScanner(ValidInventoryTypes);
        }

        for (var i = 0; i < listCount; i++)
        {
            var index = i;
            TaskManager.Enqueue(CheckIfReachTheCap);
            TaskManager.Enqueue(ClickItem);
            TaskManager.Enqueue(ClickHandIn);
            TaskManager.Enqueue(() => Service.Log.Debug($"第 {index} 轮已结束"));
        }
    }

    private bool? CheckIfReachTheCap()
    {
        if (AddonGrandCompanySupplyList == null || !HelpersOm.IsAddonAndNodesReady(AddonGrandCompanySupplyList)) return false;

        var parts = Marshal.PtrToStringUTF8((nint)AtkStage.GetSingleton()->GetStringArrayData()[32]->StringArray[2])
                           .Split('/');
        var capAmount = int.Parse(parts[1].Replace(",", ""));

        var grandCompany = UIState.Instance()->PlayerState.GrandCompany;
        if ((GrandCompany)grandCompany == GrandCompany.None)
        {
            Service.Log.Debug("玩家当前不属于任一大国防联军, 已停止");
            TaskManager.Abort();
            return true;
        }

        var companySeals = InventoryManager.Instance()->GetCompanySeals(grandCompany);

        var firstItemAmount = AddonGrandCompanySupplyList->AtkValues[265].UInt;
        if (firstItemAmount + companySeals > capAmount)
        {
            Service.Log.Debug("军票即将超限, 已停止");
            TaskManager.Abort();
        }

        return true;
    }

    private static bool? ClickItem()
    {
        if (!EzThrottler.Throttle("AutoExpertDelivery", 250)) return false;

        if (Service.Gui.GetAddonByName("GrandCompanySupplyReward") != nint.Zero) return false;
        if (Service.Gui.GetAddonByName("SelectYesno") != nint.Zero) return false;

        if (TryGetAddonByName<AddonGrandCompanySupplyList>("GrandCompanySupplyList", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(&addon->AtkUnitBase))
        {
            var handler = new ClickGrandCompanySupplyListDR();
            if (ConfigSkipWhenHQ)
            {
                for (var i = 0; i < addon->ExpertDeliveryList->ListLength; i++)
                {
                    var itemID = addon->AtkUnitBase.AtkValues[i + 425].UInt;
                    var isHQItem = HQItems.Contains(itemID);
                    if (isHQItem) continue;

                    handler.ItemEntry(i);
                }
            }
            else
                handler.ItemEntry(0);

            return true;
        }

        return false;
    }

    private static bool? ClickHandIn()
    {
        if (!EzThrottler.Throttle("AutoExpertDelivery", 250)) return false;

        if (AddonGrandCompanySupplyList != null && HelpersOm.IsAddonAndNodesReady(AddonGrandCompanySupplyList))
        {
            CloseAddon(AddonGrandCompanySupplyList);
        }

        if (TryGetAddonByName<AtkUnitBase>("GrandCompanySupplyReward", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(addon))
        {
            ClickGrandCompanySupplyReward.Using((nint)addon).Deliver();

            return true;
        }

        return false;
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

                if ((slot->Flags & InventoryItem.ItemFlags.HQ) == 0) continue;

                list.Add(item);
            }
        }

        return list;
    }

    public static void CloseAddon(AtkUnitBase* atkUnitBase, bool unknownBool = false)
    {
        AtkUnitBaseClose(atkUnitBase, (byte)(unknownBool ? 1 : 0));
    }

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(AlwaysYes);
        Service.AddonLifecycle.UnregisterListener(OnAddonSupplyList);

        base.Uninit();
    }
}
