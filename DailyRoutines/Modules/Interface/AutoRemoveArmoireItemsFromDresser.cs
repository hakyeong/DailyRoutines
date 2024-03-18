using System.Collections.Generic;
using System.Linq;
using ClickLib;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using DailyRoutines.Windows;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoRemoveArmoireItemsFromDresserTitle", "AutoRemoveArmoireItemsFromDresserDescription",
                   ModuleCategories.Interface)]
public unsafe class AutoRemoveArmoireItemsFromDresser : DailyModuleBase
{
    private static HashSet<uint>? ArmoireAvailableItems;

    public override void Init()
    {
        ArmoireAvailableItems ??= Service.Data.GetExcelSheet<Cabinet>()
                                         .Where(x => x.Category.Value.RowId is >= 1 and <= 11)
                                         .Select(x => x.Item.Value.RowId)
                                         .ToHashSet();

        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = true };
        Overlay ??= new Overlay(this);

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, "SelectYesno", AlwaysYes);
    }

    public override void ConfigUI()
    {
        if (ImGui.Button(Service.Lang.GetText("Start"))) TryRemoveItem();

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("Stop"))) TaskManager.Abort();
    }

    private bool? TryRemoveItem()
    {
        if (Service.Gui.GetAddonByName("SelectYesno") != nint.Zero) return false;

        var addon = (AtkUnitBase*)Service.Gui.GetAddonByName("MiragePrismPrismBox");
        if (addon == null) return false;

        var itemCountCurrentPage = 0;
        for (var i = 0; i < addon->AtkValuesCount; i++)
        {
            var itemID = addon->AtkValues[i].UInt;
            if (itemID == 0) break;

            itemCountCurrentPage++;
        }

        for (var i = 0U; i < itemCountCurrentPage; i++)
        {
            var currentAtkValue = addon->AtkValues[i + 100].UInt;

            var currentItemID = currentAtkValue > 100000 ? currentAtkValue % 100000 : currentAtkValue;
            Service.Log.Debug(currentItemID.ToString());
            if (ArmoireAvailableItems.Contains(currentItemID))
            {
                AgentManager.SendEvent(AgentId.MiragePrismPrismBox, 0, 3U, i);

                TaskManager.DelayNext(200);
                TaskManager.Enqueue(ClickRestoreItem);
                return true;
            }
        }

        var nextPageButton = addon->GetButtonNodeById(82);
        if (nextPageButton == null) return false;
        if (nextPageButton->IsEnabled)
        {
            AgentManager.SendEvent(AgentId.MiragePrismPrismBox, 0, 1U, 1U);
            TaskManager.DelayNext(200);
            TaskManager.Enqueue(TryRemoveItem);
            return true;
        }

        TaskManager.Enqueue(ClickNextPage);

        return true;
    }

    private bool? ClickRestoreItem()
    {
        if (TryGetAddonByName<AtkUnitBase>("ContextMenu", out var addon) && HelpersOm.IsAddonAndNodesReady(addon))
        {
            if (!HelpersOm.TryScanContextMenuText(addon, "将幻影变回道具", out var index)) return false;

            AddonManager.Callback(addon, true, 0, index, 0, 0, 0);

            TaskManager.DelayNext(200);
            TaskManager.Enqueue(TryRemoveItem);
            return true;
        }

        return false;
    }

    private bool? ClickNextPage()
    {
        var agent = AgentMiragePrismPrismBox.Instance();
        if (agent == null) return false;

        var currentTabIndex = agent->TabIndex;
        if (currentTabIndex < 10)
        {
            AgentManager.SendEvent(AgentId.MiragePrismPrismBox, 0, 0U, (uint)(currentTabIndex + 1));
            TaskManager.DelayNext(200);
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
