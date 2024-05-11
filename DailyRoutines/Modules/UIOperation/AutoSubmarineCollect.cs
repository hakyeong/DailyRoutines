using System.Collections.Generic;
using System.Numerics;
using System.Text.RegularExpressions;
using ClickLib;
using DailyRoutines.Helpers;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using DailyRoutines.Windows;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Colors;
using Dalamud.Memory;
using ECommons.Automation;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;

namespace DailyRoutines.Modules;

[PrecedingModule([typeof(AutoCutSceneSkip)])]
[ModuleDescription("AutoSubmarineCollectTitle", "AutoSubmarineCollectDescription", ModuleCategories.界面操作)]
public unsafe partial class AutoSubmarineCollect : DailyModuleBase
{
    private static AtkUnitBase* SelectString => (AtkUnitBase*)Service.Gui.GetAddonByName("SelectString");
    private static AtkUnitBase* SelectYesno => (AtkUnitBase*)Service.Gui.GetAddonByName("SelectYesno");
    // 航行结果
    private static AtkUnitBase* AirShipExplorationResult =>
        (AtkUnitBase*)Service.Gui.GetAddonByName("AirShipExplorationResult");
    // 出发详情
    private static AtkUnitBase* AirShipExplorationDetail =>
        (AtkUnitBase*)Service.Gui.GetAddonByName("AirShipExplorationDetail");
    private static AtkUnitBase* CompanyCraftSupply => (AtkUnitBase*)Service.Gui.GetAddonByName("CompanyCraftSupply");

    private static TaskManager? RepairTaskManager;

    private static readonly HashSet<uint> CompanyWorkshopZones = [423, 424, 425, 653, 984];
    private static string RequisiteMaterialsName = string.Empty;
    private static int? RequisiteMaterials;

    public override void Init()
    {
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 30000, ShowDebug = false };
        RepairTaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 10000, ShowDebug = false };

        Overlay ??= new Overlay(this);
        Overlay.Flags |= ImGuiWindowFlags.NoMove;

        RequisiteMaterialsName = LuminaCache.GetRow<Item>(10373).Name.RawString;

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "SelectYesno", AlwaysYes);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "AirShipExplorationResult", OnExplorationResult);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, "SelectString", OnAddonSelectString);
        Service.LogMessageManager.Register(OnLogMessages);
    }

    public override void OverlayUI()
    {
        if (SelectString == null)
        {
            Overlay.IsOpen = false;
            return;
        }

        var pos = new Vector2(SelectString->GetX() + 6, SelectString->GetY() - ImGui.GetWindowSize().Y + 6);
        ImGui.SetWindowPos(pos);

        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(ImGuiColors.DalamudYellow, Service.Lang.GetText("AutoSubmarineCollectTitle"));

        ImGui.SameLine();

        ImGui.BeginDisabled(TaskManager.IsBusy);
        if (ImGui.Button(Service.Lang.GetText("Start"))) GetSubmarineInfos();
        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("Stop"))) TaskManager.Abort();

        RequisiteMaterials ??= InventoryManager.Instance()->GetInventoryItemCount(10373);

        ImGui.SameLine();
        ImGui.Text($"{RequisiteMaterialsName}:");

        ImGui.SameLine();
        ImGui.TextColored(RequisiteMaterials < 20 ? ImGuiColors.DPSRed : ImGuiColors.HealerGreen,
                          RequisiteMaterials.ToString());

        if (EzThrottler.Throttle("AutoSubmarineCollectOverlay-RequestItemAmount", 1000))
        {
            var inventoryManager = InventoryManager.Instance();
            RequisiteMaterials = inventoryManager->GetInventoryItemCount(10373);
        }
    }

    private bool? GetSubmarineInfos()
    {
        // 还在看动画
        if (Service.Condition[ConditionFlag.OccupiedInCutSceneEvent] ||
            Service.Condition[ConditionFlag.WatchingCutscene78]) return false;

        #region CheckNecessaryItems

        var inventoryManager = InventoryManager.Instance();
        if (inventoryManager->GetInventoryItemCount(10373) < 20)
        {
            var message = new SeStringBuilder().Append(DRPrefix()).Append(" ")
                                               .Append(Service.Lang.GetSeString(
                                                           "AutoSubmarineCollect-LackSpecificItems",
                                                           SeString.CreateItemLink(10373))).Build();
            Service.Chat.Print(message);

            TaskManager.Abort();
            return true;
        }

        if (inventoryManager->GetInventoryItemCount(10155) < 15)
        {
            var message = new SeStringBuilder().Append(DRPrefix()).Append(" ")
                                               .Append(Service.Lang.GetSeString(
                                                           "AutoSubmarineCollect-LackSpecificItems",
                                                           SeString.CreateItemLink(10155))).Build();
            Service.Chat.Print(message);

            TaskManager.Abort();
            return true;
        }

        #endregion

        if (SelectString == null || !IsAddonAndNodesReady(SelectString)) return false;
        if (!ClickHelper.SelectString("探索完成"))
        {
            TaskManager.Abort();
            return true;
        }

        TaskManager.Abort();
        TaskManager.DelayNext(2000);
        TaskManager.Enqueue(CommenceSubmarineVoyage);

        return true;
    }

    private bool? CommenceSubmarineVoyage()
    {
        if (AirShipExplorationDetail == null || !IsAddonAndNodesReady(AirShipExplorationDetail)) return false;

        AddonHelper.Callback(AirShipExplorationDetail, true, 0);
        AirShipExplorationDetail->Close(true);

        TaskManager.Abort();
        TaskManager.DelayNext(3000);
        TaskManager.Enqueue(GetSubmarineInfos);

        return false;
    }

    private bool? ReadyToRepairSubmarines()
    {
        if (AirShipExplorationDetail != null)
        {
            AirShipExplorationDetail->Close(true);
            return false;
        }

        if (AirShipExplorationResult != null)
        {
            AirShipExplorationResult->Close(true);
            return false;
        }

        if (CompanyCraftSupply != null && IsAddonAndNodesReady(CompanyCraftSupply))
        {
            RepairTaskManager.Enqueue(RepairSubmarines);
            RepairTaskManager.DelayNext(20);
            RepairTaskManager.Enqueue(() => AddonHelper.Callback(CompanyCraftSupply, true, 5));
            RepairTaskManager.Enqueue(ClickPreviousVoyageLog); return true;
        }

        if (SelectString != null && IsAddonAndNodesReady(SelectString))
        {
            if (!ClickHelper.SelectString("修理")) return false;

            SelectString->Close(true);

            RepairTaskManager.Enqueue(RepairSubmarines);
            RepairTaskManager.DelayNext(20);
            RepairTaskManager.Enqueue(() => AddonHelper.Callback(CompanyCraftSupply, true, 5));
            RepairTaskManager.Enqueue(ClickPreviousVoyageLog);
            RepairTaskManager.DelayNext(100);
            RepairTaskManager.Enqueue(CommenceSubmarineVoyage);
            return true;
        }

        return false;
    }

    private static bool? RepairSubmarines()
    {
        if (!EzThrottler.Throttle("AutoSubmarineCollect-Repair", 100)) return false;
        if (SelectYesno != null) return false;
        if (CompanyCraftSupply == null || !IsAddonAndNodesReady(CompanyCraftSupply)) return false;

        for (var i = 0; i < 4; i++)
        {
            var endurance = CompanyCraftSupply->AtkValues[3 + (8 * i)].UInt;
            if (endurance <= 0)
            {
                AgentHelper.SendEvent(AgentId.SubmersibleParts, 0, 3, 0, i, 0, 0, 0);
                return false;
            }
        }

        return true;
    }

    private bool? ClickPreviousVoyageLog()
    {
        if (AirShipExplorationDetail != null && IsAddonAndNodesReady(AirShipExplorationDetail))
        {
            RepairTaskManager.Abort();
            RepairTaskManager.Enqueue(CommenceSubmarineVoyage);

            return true;
        }

        if (SelectString == null || !IsAddonAndNodesReady(SelectString)) return false;

        if (!ClickHelper.SelectString("上次的远航报告")) return false;

        return true;
    }

    private void OnLogMessages(uint logMessageID, ushort logKind)
    {
        switch (logMessageID)
        {
            case 4290:
                TaskManager.Abort();
                RepairTaskManager.Abort();
                RepairTaskManager.Enqueue(ReadyToRepairSubmarines);
                break;
            case 4276:
                TaskManager.Abort();
                RepairTaskManager.Abort();
                break;
        }
    }

    private void OnExplorationResult(AddonEvent type, AddonArgs args)
    {
        if (AirShipExplorationResult == null || !IsAddonAndNodesReady(AirShipExplorationResult)) return;

        AddonHelper.Callback(AirShipExplorationResult, true, 1);
        if (TaskManager.IsBusy) AirShipExplorationResult->IsVisible = false;
    }

    private void OnAddonSelectString(AddonEvent type, AddonArgs args)
    {
        if (!EzThrottler.Throttle("AutoSubmarineCollectOverlay")) return;
        if (!CompanyWorkshopZones.Contains(Service.ClientState.TerritoryType)) return;

        Overlay.IsOpen = false;

        if (SelectString == null) return;
        var title = MemoryHelper.ReadStringNullTerminated((nint)SelectString->AtkValues[2].String);
        if (string.IsNullOrWhiteSpace(title) || !title.Contains("请选择潜水艇")) return;
        Overlay.IsOpen = true;
    }

    private void AlwaysYes(AddonEvent type, AddonArgs args)
    {
        if (!TaskManager.IsBusy) return;
        Click.SendClick("select_yes");
    }

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnExplorationResult);
        Service.AddonLifecycle.UnregisterListener(AlwaysYes);
        Service.AddonLifecycle.UnregisterListener(OnAddonSelectString);
        Service.LogMessageManager.Unregister(OnLogMessages);

        RepairTaskManager?.Abort();
        RepairTaskManager = null;

        base.Uninit();
    }


    [GeneratedRegex("探索机体数：\\d+/(\\d+)")]
    private static partial Regex SubmarineInfoRegex();
}
