using System.Collections.Generic;
using System.Linq;
using ClickLib;
using DailyRoutines.Helpers;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using DailyRoutines.Windows;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using ECommons.Automation;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoRemoveArmoireItemsFromDresserTitle", "AutoRemoveArmoireItemsFromDresserDescription",
                   ModuleCategories.InterfaceExpand)]
public unsafe class AutoRemoveArmoireItemsFromDresser : DailyModuleBase
{
    private static AtkUnitBase* AddonMiragePrismPrismBox => (AtkUnitBase*)Service.Gui.GetAddonByName("MiragePrismPrismBox");

    private static HashSet<uint>? ArmoireAvailableItems;

    public override void Init()
    {
        ArmoireAvailableItems ??= LuminaCache.Get<Cabinet>()
                                         .Where(x => x.Category.Value.RowId is >= 1 and <= 11)
                                         .Select(x => x.Item.Value.RowId)
                                         .ToHashSet();

        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = true };
        Overlay ??= new Overlay(this);

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, "SelectYesno", AlwaysYes);
    }

    public override void ConfigUI()
    {
        ImGui.BeginDisabled(TaskManager.IsBusy);
        if (ImGui.Button(Service.Lang.GetText("Start")))
        {
            if (AddonMiragePrismPrismBox == null) return;
            TaskManager.Enqueue(() => AddonHelper.Callback(AddonMiragePrismPrismBox, true, 0U, 0U));
            TaskManager.DelayNext(20);
            TaskManager.Enqueue(TryRemoveItem);
        }
        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("Stop"))) TaskManager.Abort();
    }

    private bool? TryRemoveItem()
    {
        if (!EzThrottler.Throttle("AutoRemoveArmoireItemsFromDresser", 100)) return false;
        if (Service.Gui.GetAddonByName("SelectYesno") != nint.Zero) return false;

        if (AddonMiragePrismPrismBox == null) return false;

        var itemCountCurrentPage = 0;
        for (var i = 0; i < AddonMiragePrismPrismBox->AtkValuesCount; i++)
        {
            var itemID = AddonMiragePrismPrismBox->AtkValues[i].UInt;
            if (itemID == 0) break;

            itemCountCurrentPage++;
        }

        for (var i = 0U; i < itemCountCurrentPage; i++)
        {
            var currentAtkValue = AddonMiragePrismPrismBox->AtkValues[i + 100].UInt;

            var currentItemID = currentAtkValue > 100000 ? currentAtkValue % 100000 : currentAtkValue;
            Service.Log.Debug(currentItemID.ToString());
            if (ArmoireAvailableItems.Contains(currentItemID))
            {
                AddonHelper.Callback(AddonMiragePrismPrismBox, true, 3U, i);
                
                TaskManager.Enqueue(ClickRestoreItem);
                return true;
            }
        }

        var nextPageButton = AddonMiragePrismPrismBox->GetButtonNodeById(82);
        if (nextPageButton == null) return false;
        if (nextPageButton->IsEnabled)
        {
            AddonHelper.Callback(AddonMiragePrismPrismBox, true, 1U, 1U);

            TaskManager.Enqueue(TryRemoveItem);
            return true;
        }

        TaskManager.Enqueue(ClickNextCategory);

        return true;
    }

    private bool? ClickRestoreItem()
    {
        if (!EzThrottler.Throttle("AutoRemoveArmoireItemsFromDresser", 100)) return false;
        if (!ClickHelper.ContextMenu("将幻影变回道具")) return false;

        TaskManager.Enqueue(TryRemoveItem);
        return true;
    }

    private bool? ClickNextCategory()
    {
        if (!EzThrottler.Throttle("AutoRemoveArmoireItemsFromDresser", 100)) return false;
        var agent = AgentMiragePrismPrismBox.Instance();
        if (agent == null) return false;
        if (AddonMiragePrismPrismBox == null) return false;

        var currentTabIndex = agent->TabIndex;
        if (currentTabIndex < 10)
        {
            AddonHelper.Callback(AddonMiragePrismPrismBox, true, 0U, (uint)(currentTabIndex + 1));

            TaskManager.Enqueue(TryRemoveItem);
        }
        else
            TaskManager.Abort();

        return true;
    }

    private void AlwaysYes(AddonEvent type, AddonArgs args)
    {
        if (!TaskManager.IsBusy) return;
        Click.SendClick("select_yes");
    }

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(AlwaysYes);

        base.Uninit();
    }
}
