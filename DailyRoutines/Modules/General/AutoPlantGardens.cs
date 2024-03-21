using System;
using System.Collections.Generic;
using System.Linq;
using ClickLib;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Memory;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoPlantGardensTitle", "AutoPlantGardensDescription", ModuleCategories.General)]
public class AutoPlantGardens : DailyModuleBase
{
    private static Dictionary<uint, Item> Seeds = [];
    private static Dictionary<uint, Item> Soils = [];

    private static uint SelectedSeed;
    private static uint SelectedSoil;

    private static string searchFilterSeed = string.Empty;

    public override void Init()
    {
        Seeds = Service.Data.GetExcelSheet<Item>()
                             .Where(x => x.FilterGroup == 20)
                             .ToDictionary(x => x.RowId, x => x);
        Soils = Service.Data.GetExcelSheet<Item>()
                           .Where(x => x.FilterGroup == 21)
                           .ToDictionary(x => x.RowId, x => x);

        AddConfig(this, "SelectedSeed", SelectedSeed);
        AddConfig(this, "SelectedSoil", SelectedSoil);

        SelectedSeed = GetConfig<uint>(this, "SelectedSeed");
        SelectedSoil = GetConfig<uint>(this, "SelectedSoil");

        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = false };

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "HousingGardening", OnAddon);
    }

    private unsafe void OnAddon(AddonEvent type, AddonArgs args)
    {
        if (SelectedSeed == 0 || SelectedSoil == 0) return;
        var inventoryManager = InventoryManager.Instance();
        if (inventoryManager->GetInventoryItemCount(SelectedSeed) == 0 || inventoryManager->GetInventoryItemCount(SelectedSoil) == 0) return;

        TaskManager.Abort();

        TaskManager.Enqueue(() => AgentManager.SendEvent(AgentId.HousingPlant, 0, 2, 0U, 0, 0, 1U));

        TaskManager.DelayNext(10);
        TaskManager.Enqueue(() => FillContextMenu(Soils[SelectedSoil].Name.ExtractText()));

        TaskManager.DelayNext(10);
        TaskManager.Enqueue(() => AgentManager.SendEvent(AgentId.HousingPlant, 0, 2, 1U, 0, 0, 1U));

        TaskManager.DelayNext(10);
        TaskManager.Enqueue(() => FillContextMenu(Seeds[SelectedSeed].Name.ExtractText()));

        TaskManager.DelayNext(10);
        TaskManager.Enqueue(() => AgentManager.SendEvent(AgentId.HousingPlant, 0, 0, 0, 0, 0, 0));

        TaskManager.DelayNext(10);
        TaskManager.Enqueue(() => Click.TrySendClick("select_yes"));
    }

    public override void ConfigUI()
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(ImGuiColors.DalamudOrange, $"{Service.Lang.GetText("AutoPlantGardens-Seed")}:");

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
                    UpdateConfig(this, "SelectedSeed", SelectedSeed);
                }

                if (ImGui.IsWindowAppearing() && SelectedSeed == item.Key)
                    ImGui.SetScrollHereY();
            }

            ImGui.EndCombo();
        }

        ImGui.SameLine();
        ImGui.Spacing();

        ImGui.SameLine();
        ImGui.TextColored(ImGuiColors.DalamudOrange, $"{Service.Lang.GetText("AutoPlantGardens-Soil")}:");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(150f * ImGuiHelpers.GlobalScale);
        if (ImGui.BeginCombo("###Soils", SelectedSoil == 0 ? string.Empty : Soils[SelectedSoil].Name.ExtractText(), ImGuiComboFlags.HeightLarge))
        {
            foreach (var item in Soils)
            {
                if (ImGui.Selectable($"{item.Value.Name.ExtractText()}", SelectedSoil == item.Key))
                {
                    SelectedSoil = item.Key;
                    UpdateConfig(this, "SelectedSoil", SelectedSoil);
                }

                if (ImGui.IsWindowAppearing() && SelectedSoil == item.Key)
                    ImGui.SetScrollHereY();
            }

            ImGui.EndCombo();
        }
    }

    private unsafe bool? FillContextMenu(string itemNameToSelect)
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
            Service.Log.Debug($"{iconID} {itemName}");

            if (itemName == itemNameToSelect)
            {
                AddonManager.Callback(addon, true, 0, i, iconID, 0U, 0);
                return true;
            }
        }

        TaskManager.Abort();
        return true;
    }

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddon);

        base.Uninit();
    }
}
