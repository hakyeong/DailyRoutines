using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using ClickLib.Clicks;
using DailyRoutines.Clicks;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using DailyRoutines.Windows;
using Dalamud.Game.AddonLifecycle;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoExpertDeliveryTitle", "AutoExpertDeliveryDescription", ModuleCategories.Interface)]
public unsafe class AutoExpertDelivery : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithConfigUI => false;
    internal static Overlay? Overlay { get; private set; }

    private static TaskManager? TaskManager;
    private static bool IsOnProcess;
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

    public void Init()
    {
        Service.Config.AddConfig(this, "SkipWhenHQ", ConfigSkipWhenHQ);
        ConfigSkipWhenHQ = Service.Config.GetConfig<bool>(this, "SkipWhenHQ");

        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = false };
        Overlay ??= new Overlay(this);

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "GrandCompanySupplyList", OnAddonSupplyList);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "GrandCompanySupplyList", OnAddonSupplyList);
    }

    public void ConfigUI() { }

    public void OverlayUI()
    {
        var addon = (AtkUnitBase*)Service.Gui.GetAddonByName("GrandCompanySupplyList");
        if (addon == null) return;

        var pos = new Vector2(addon->GetX() - ImGui.GetWindowSize().X, addon->GetY() + 6);
        ImGui.SetWindowPos(pos);

        ImGui.TextColored(ImGuiColors.DalamudYellow, Service.Lang.GetText("AutoExpertDeliveryTitle"));
        ImGui.PushTextWrapPos(300f * ImGuiHelpers.GlobalScale);
        ImGui.TextDisabled(Service.Lang.GetText("AutoExpertDeliveryDescription"));
        ImGui.PopTextWrapPos();

        ImGui.Separator();

        ImGui.BeginDisabled(IsOnProcess);
        if (ImGui.Checkbox("跳过 HQ 物品", ref ConfigSkipWhenHQ))
            Service.Config.UpdateConfig(this, "SkipWhenHQ", ConfigSkipWhenHQ);

        if (ImGui.Button(Service.Lang.GetText("AutoExpertDelivery-Start"))) StartHandOver();
        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("AutoExpertDelivery-Stop"))) EndHandOver();
    }

    private static void OnAddonSupplyList(AddonEvent type, AddonArgs args)
    {
        Overlay.IsOpen = type switch
        {
            AddonEvent.PostSetup => true,
            AddonEvent.PreFinalize => false,
            _ => Overlay.IsOpen
        };
    }

    private static void OnAddonSupplyReward(AddonEvent type, AddonArgs args)
    {
        if (TryGetAddonByName<AtkUnitBase>("GrandCompanySupplyReward", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(addon))
        {
            var handler = new ClickGrandCompanySupplyReward();
            handler.Deliver();
        }
    }

    private static bool? StartHandOver()
    {
        if (Service.Gui.GetAddonByName("GrandCompanySupplyReward") != nint.Zero) return false;

        var handler = new ClickGrandCompanySupplyListDR();
        handler.ExpertDelivery();

        if (IsSealsReachTheCap())
        {
            EndHandOver();
            return true;
        }

        if (!IsOnProcess)
        {
            if (ConfigSkipWhenHQ)
            {
                HQItems.Clear();
                HQItems = InventoryScanner(ValidInventoryTypes);
            }

            Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "GrandCompanySupplyReward",
                                                    OnAddonSupplyReward);
        }

        TaskManager.Enqueue(ClickItem);
        IsOnProcess = true;

        return true;
    }

    private static void EndHandOver()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddonSupplyReward);
        TaskManager?.Abort();
        IsOnProcess = false;
    }

    // (即将)达到限额 - true
    private static bool IsSealsReachTheCap()
    {
        if (TryGetAddonByName<AddonGrandCompanySupplyList>("GrandCompanySupplyList", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(&addon->AtkUnitBase))
        {
            if (addon->ExpertDeliveryList->ListLength == 0) return true;

            var parts = Marshal.PtrToStringUTF8((nint)AtkStage.GetSingleton()->GetStringArrayData()[32]->StringArray[2])
                               .Split('/');
            var currentAmount = int.Parse(parts[0].Replace(",", ""));
            var capAmount = int.Parse(parts[1].Replace(",", ""));

            var firstItemAmount = addon->AtkUnitBase.AtkValues[265].UInt;
            return firstItemAmount + currentAmount > capAmount;
        }

        return true;
    }

    private static bool? ClickItem()
    {
        if (TryGetAddonByName<AddonGrandCompanySupplyList>("GrandCompanySupplyList", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(&addon->AtkUnitBase))
        {
            var handler = new ClickGrandCompanySupplyListDR();
            if (ConfigSkipWhenHQ)
            {
                var isOnlyHaveHQItems = true;
                for (var i = 0; i < addon->ExpertDeliveryList->ListLength; i++)
                {
                    var isHQItem = HQItems.Contains(addon->AtkUnitBase.AtkValues[i + 425].UInt);
                    if (isHQItem) continue;

                    handler.ItemEntry(i);
                    isOnlyHaveHQItems = false;
                }

                if (isOnlyHaveHQItems)
                {
                    EndHandOver();
                    return true;
                }
            }
            else
                handler.ItemEntry(0);

            TaskManager.Enqueue(StartHandOver);
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

    public void Uninit()
    {
        EndHandOver();

        if (P.WindowSystem.Windows.Contains(Overlay)) P.WindowSystem.RemoveWindow(Overlay);
        Overlay = null;

        Service.AddonLifecycle.UnregisterListener(OnAddonSupplyList);
    }
}
