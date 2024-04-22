using ClickLib;
using DailyRoutines.Managers;
using Dalamud.Memory;
using Dalamud.Utility.Signatures;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Lumina.Excel.GeneratedSheets;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using System;
using DailyRoutines.Helpers;
using DailyRoutines.Infos;
using Dalamud.Game.ClientState.Conditions;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoGardensWorkTitle", "AutoGardensWorkDescription", ModuleCategories.General)]
public unsafe class AutoGardensWork : DailyModuleBase
{
    [Signature("E8 ?? ?? ?? ?? 44 0F B6 C3 48 8B D0")]
    private readonly delegate* unmanaged<ulong, GameObject*> GetGameObjectFromObjectID;

    private static Dictionary<uint, Item> Seeds = [];
    private static Dictionary<uint, Item> Soils = [];
    private static Dictionary<uint, Item> Fertilizers = [];

    private static readonly InventoryType[] InventoryTypes =
    [
        InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3,
        InventoryType.Inventory4
    ];

    private static uint SelectedSeed;
    private static uint SelectedSoil;
    private static uint SelectedFertilizer;

    private static uint[] Gardens = [];
    private static string searchFilterSeed = string.Empty;

    public override void Init()
    {
        var sheet = LuminaCache.Get<Item>();
        Seeds = sheet
                .Where(x => x.FilterGroup == 20)
                .ToDictionary(x => x.RowId, x => x);
        Soils = sheet
                .Where(x => x.FilterGroup == 21)
                .ToDictionary(x => x.RowId, x => x);
        Fertilizers = sheet
                      .Where(x => x.FilterGroup == 22)
                      .ToDictionary(x => x.RowId, x => x);

        AddConfig("SelectedSeed", SelectedSeed);
        AddConfig("SelectedSoil", SelectedSoil);
        AddConfig("SelectedFertilizer", SelectedFertilizer);

        SelectedSeed = GetConfig<uint>("SelectedSeed");
        SelectedSoil = GetConfig<uint>("SelectedSoil");
        SelectedFertilizer = GetConfig<uint>("SelectedFertilizer");

        Service.Hook.InitializeFromAttributes(this);

        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 10000, ShowDebug = false };
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "HousingGardening", OnAddon);
    }

    public override void ConfigUI()
    {
        ImGui.BeginDisabled(TaskManager.IsBusy);
        ImGui.BeginGroup();

        // 自动种植
        ImGui.PushID("AutoPlant");
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(ImGuiColors.DalamudOrange, $"{Service.Lang.GetText("AutoGardensWork-AutoPlant")}:");

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("Start")))
            StartPlant();

        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(ImGuiColors.DalamudOrange, $"{Service.Lang.GetText("AutoGardensWork-Seed")}:");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(150f * ImGuiHelpers.GlobalScale);
        if (ImGui.BeginCombo("###Seeds", SelectedSeed == 0 ? string.Empty : Seeds[SelectedSeed].Name.ExtractText(), ImGuiComboFlags.HeightLarge))
        {
            ImGui.InputTextWithHint("###SearchSeedInput", Service.Lang.GetText("PleaseSearch"),
                                    ref searchFilterSeed, 100);

            foreach (var item in Seeds)
            {
                var itemName = item.Value.Name.ExtractText();
                if (!string.IsNullOrWhiteSpace(searchFilterSeed) && !itemName.Contains(searchFilterSeed, StringComparison.OrdinalIgnoreCase)) continue;

                if (ImGui.Selectable($"{itemName}", SelectedSeed == item.Key))
                {
                    SelectedSeed = item.Key;
                    UpdateConfig("SelectedSeed", SelectedSeed);
                }

                if (ImGui.IsWindowAppearing() && SelectedSeed == item.Key)
                    ImGui.SetScrollHereY();
            }

            ImGui.EndCombo();
        }

        ImGui.SameLine();
        ImGui.TextColored(ImGuiColors.DalamudOrange, $"{Service.Lang.GetText("AutoGardensWork-Soil")}:");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(150f * ImGuiHelpers.GlobalScale);
        if (ImGui.BeginCombo("###Soils", SelectedSoil == 0 ? string.Empty : Soils[SelectedSoil].Name.ExtractText(), ImGuiComboFlags.HeightLarge))
        {
            foreach (var item in Soils)
            {
                if (ImGui.Selectable($"{item.Value.Name.ExtractText()}", SelectedSoil == item.Key))
                {
                    SelectedSoil = item.Key;
                    UpdateConfig("SelectedSoil", SelectedSoil);
                }

                if (ImGui.IsWindowAppearing() && SelectedSoil == item.Key)
                    ImGui.SetScrollHereY();
            }

            ImGui.EndCombo();
        }
        ImGui.PopID();

        // 自动收获
        ImGui.PushID("AutoGather");
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(ImGuiColors.DalamudOrange, $"{Service.Lang.GetText("AutoGardensWork-AutoGather")}:");

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("Start")))
            StartGather();
        ImGui.PopID();

        // 自动施肥
        ImGui.PushID("AutoFertilize");
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(ImGuiColors.DalamudOrange, $"{Service.Lang.GetText("AutoGardensWork-AutoFertilize")}:");

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("Start")))
            StartFertilize();

        ImGui.SameLine();
        ImGui.TextColored(ImGuiColors.DalamudOrange, $"{Service.Lang.GetText("AutoGardensWork-Fertilizer")}:");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(150f * ImGuiHelpers.GlobalScale);
        if (ImGui.BeginCombo("###Fertilizer", SelectedFertilizer == 0 ? string.Empty : Fertilizers[SelectedFertilizer].Name.ExtractText(), ImGuiComboFlags.HeightLarge))
        {
            foreach (var item in Fertilizers)
            {
                if (ImGui.Selectable($"{item.Value.Name.ExtractText()}", SelectedFertilizer == item.Key))
                {
                    SelectedFertilizer = item.Key;
                    UpdateConfig("SelectedFertilizer", SelectedFertilizer);
                }

                if (ImGui.IsWindowAppearing() && SelectedFertilizer == item.Key)
                    ImGui.SetScrollHereY();
            }

            ImGui.EndCombo();
        }

        // 自动护理
        ImGui.PushID("AutoTend");
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(ImGuiColors.DalamudOrange, $"{Service.Lang.GetText("AutoGardensWork-AutoTend")}:");

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("Start")))
            StartTend();
        ImGui.PopID();

        ImGui.EndGroup();
        ImGui.EndDisabled();

        var groupSize = ImGui.GetItemRectSize();

        ImGui.SameLine();
        ImGui.Spacing();

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("Stop"), groupSize with { X = 80f * ImGuiHelpers.GlobalScale }))
            TaskManager.Abort();
    }

    private void OnAddon(AddonEvent type, AddonArgs args)
    {
        if (SelectedSeed == 0 || SelectedSoil == 0) return;
        var inventoryManager = InventoryManager.Instance();
        if (inventoryManager->GetInventoryItemCount(SelectedSeed) == 0 || inventoryManager->GetInventoryItemCount(SelectedSoil) == 0) return;

        TaskManager.EnqueueImmediate(() => AgentManager.SendEvent(AgentId.HousingPlant, 0, 2, 0U, 0, 0, 1U));

        TaskManager.DelayNextImmediate(10);
        TaskManager.EnqueueImmediate(() => FillContextMenu(Soils[SelectedSoil].Name.ExtractText()));

        TaskManager.DelayNextImmediate(10);
        TaskManager.EnqueueImmediate(() => AgentManager.SendEvent(AgentId.HousingPlant, 0, 2, 1U, 0, 0, 1U));

        TaskManager.DelayNextImmediate(10);
        TaskManager.EnqueueImmediate(() => FillContextMenu(Seeds[SelectedSeed].Name.ExtractText()));

        TaskManager.DelayNextImmediate(10);
        TaskManager.EnqueueImmediate(() => AgentManager.SendEvent(AgentId.HousingPlant, 0, 0, 0, 0, 0, 0));

        TaskManager.DelayNextImmediate(10);
        TaskManager.EnqueueImmediate(() => Click.TrySendClick("select_yes"));
    }

    private void StartGather()
    {
        ObtainGardensAround();

        var localPlayer = (GameObject*)Service.ClientState.LocalPlayer.Address;
        if (localPlayer == null) return;

        foreach (var objID in Gardens)
        {
            var gameObj = GetGameObjectFromObjectID(objID);
            var objDistance = HelpersOm.GetGameDistanceFromObject(localPlayer, gameObj);
            if (objDistance > 2.5) continue;

            TaskManager.Enqueue(() => InteractWithGarden(gameObj));
            TaskManager.Enqueue(() => ClickEntryByText("收获"));
        }
    }

    private void StartTend()
    {
        ObtainGardensAround();

        var localPlayer = (GameObject*)Service.ClientState.LocalPlayer.Address;
        if (localPlayer == null) return;

        foreach (var objID in Gardens)
        {
            var gameObj = GetGameObjectFromObjectID(objID);
            var objDistance = HelpersOm.GetGameDistanceFromObject(localPlayer, gameObj);
            if (objDistance > 2.5) continue;

            TaskManager.Enqueue(() => InteractWithGarden(gameObj));
            TaskManager.Enqueue(() => ClickEntryByText("护理"));
        }
    }

    private void StartPlant()
    {
        ObtainGardensAround();

        var localPlayer = (GameObject*)Service.ClientState.LocalPlayer.Address;
        if (localPlayer == null) return;

        foreach (var objID in Gardens)
        {
            var gameObj = GetGameObjectFromObjectID(objID);
            var objDistance = HelpersOm.GetGameDistanceFromObject(localPlayer, gameObj);
            if (objDistance > 2.5) continue;

            TaskManager.Enqueue(() => InteractWithGarden(gameObj));
            TaskManager.Enqueue(() => ClickEntryByText("播种"));
            TaskManager.DelayNext(250);
        }
    }

    private void StartFertilize()
    {
        ObtainGardensAround();

        var localPlayer = (GameObject*)Service.ClientState.LocalPlayer.Address;
        if (localPlayer == null) return;

        foreach (var objID in Gardens)
        {
            var gameObj = GetGameObjectFromObjectID(objID);
            var objDistance = HelpersOm.GetGameDistanceFromObject(localPlayer, gameObj);
            if (objDistance > 2.5) continue;

            TaskManager.Enqueue(() => InteractWithGarden(gameObj));
            TaskManager.Enqueue(() => ClickEntryByText("施肥"));
            TaskManager.Enqueue(CheckFertilizerState);
            TaskManager.Enqueue(ClickFertilizer);
            TaskManager.Enqueue(() => !Service.Condition[ConditionFlag.OccupiedInQuestEvent]);
        }
    }

    private static void ObtainGardensAround()
    {
        var tempSet = new HashSet<uint>();
        foreach (var obj in Service.ObjectTable.Where(x => x.DataId == 2003757))
        {
            tempSet.Add(obj.ObjectId);
        }

        Gardens = [.. tempSet];
    }

    private static bool? CheckFertilizerState()
    {
        var selectString = Service.Gui.GetAddonByName("SelectString");
        var inventory = (AtkUnitBase*)Service.Gui.GetAddonByName("Inventory");
        var inventoryLarge = (AtkUnitBase*)Service.Gui.GetAddonByName("InventoryLarge");
        var inventoryExpansion = (AtkUnitBase*)Service.Gui.GetAddonByName("InventoryExpansion");

        if (selectString != nint.Zero) return false;

        if (inventory->IsVisible || inventoryLarge->IsVisible || inventoryExpansion->IsVisible ||
            !Service.Condition[ConditionFlag.OccupiedInQuestEvent])
        {
            return true;
        }

        return false;
    }

    private bool? ClickFertilizer()
    {
        if (Service.Gui.GetAddonByName("SelectString") != nint.Zero) return false;

        if (!Service.Condition[ConditionFlag.OccupiedInQuestEvent])
        {
            return true;
        }

        if (SelectedFertilizer == 0)
        {
            TaskManager.Abort();
            return true;
        }

        if (Service.Gui.GetAddonByName("Inventory") == nint.Zero) return false;
        var agent = AgentInventoryContext.Instance();
        if (agent == null) return false;

        var inventoryManager = InventoryManager.Instance();
        if (inventoryManager == null) return false;

        InventoryType? foundType = null;
        foreach (var type in InventoryTypes)
        {
            if (inventoryManager->GetItemCountInContainer(SelectedFertilizer, type) != 0)
            {
                foundType = type;
                break;
            }
        }
        if (foundType == null) return false;

        var container = inventoryManager->GetInventoryContainer((InventoryType)foundType);
        if (container == null) return false;

        int? foundSlot = null;
        for (var i = 0; i < container->Size; i++)
        {
            var slot = container->GetInventorySlot(i);
            if (slot->ItemID == SelectedFertilizer)
            {
                foundSlot = i;
                break;
            }
        }
        if (foundSlot == null) return false;

        var agentInventory = AgentModule.Instance()->GetAgentByInternalId(AgentId.Inventory);
        if (agentInventory == null) return false;

        agent->OpenForItemSlot((InventoryType)foundType, (int)foundSlot, agentInventory->AddonId);

        TaskManager.InsertDelayNext(20);
        TaskManager.Insert(() => ClickContextMenuByText("施肥"));

        return true;
    }

    private static bool? ClickContextMenuByText(string text)
    {
        if (!Service.Condition[ConditionFlag.OccupiedInQuestEvent]) return true;

        if (TryGetAddonByName<AtkUnitBase>("ContextMenu", out var addon) && HelpersOm.IsAddonAndNodesReady(addon))
        {
            if (!HelpersOm.TryScanContextMenuText(addon, text, out var index))
            {
                addon->FireCloseCallback();
                addon->Close(true);
                return true;
            }

            AddonManager.Callback(addon, true, 0, index, 0U, 0, 0);
        }

        return false;
    }

    private bool? FillContextMenu(string itemNameToSelect)
    {
        if (!TryGetAddonByName<AtkUnitBase>("ContextIconMenu", out var addon) ||
            !HelpersOm.IsAddonAndNodesReady(addon)) return false;

        var validAtkValuesCount = addon->AtkValuesCount - 11;
        if (validAtkValuesCount % 6 != 0) return false;
        var entryAmount = addon->AtkValuesCount / 6;

        for (var i = 0; i < entryAmount; i++)
        {
            var iconID = addon->AtkValues[11 + i * 6].UInt;
            var itemName = MemoryHelper.ReadSeStringNullTerminated((nint)addon->AtkValues[13 + i * 6].String).ExtractText();

            if (itemName == itemNameToSelect)
            {
                AddonManager.Callback(addon, true, 0, i, iconID, 0U, 0);
                return true;
            }
        }

        TaskManager.Abort();
        return true;
    }

    private static bool? InteractWithGarden(GameObject* gameObj)
    {
        if (IsOccupied()) return false;

        var targetSystem = TargetSystem.Instance();
        targetSystem->Target = gameObj;
        targetSystem->InteractWithObject(gameObj);

        return true;
    }

    private static bool? ClickEntryByText(string text)
    {
        if (TryGetAddonByName<AtkUnitBase>("SelectString", out var addon) && HelpersOm.IsAddonAndNodesReady(addon))
        {
            var content = MemoryHelper.ReadStringNullTerminated((nint)addon->AtkValues[2].String);
            if (!HelpersOm.TryScanSelectStringText(addon, text, out var index))
            {
                HelpersOm.TryScanSelectStringText(addon, "取消", out index);
                return Click.TrySendClick($"select_string{index + 1}");
            }

            if (Click.TrySendClick($"select_string{index + 1}")) return true;
        }

        return false;
    }

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddon);

        base.Uninit();
    }
}
