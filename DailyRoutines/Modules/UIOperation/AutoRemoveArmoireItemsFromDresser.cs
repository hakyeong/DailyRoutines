using System.Collections.Generic;
using System.Linq;
using DailyRoutines.Helpers;
using DailyRoutines.Managers;
using ECommons.Automation.LegacyTaskManager;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoRemoveArmoireItemsFromDresserTitle", "AutoRemoveArmoireItemsFromDresserDescription",
                   ModuleCategories.界面操作)]
public unsafe class AutoRemoveArmoireItemsFromDresser : DailyModuleBase
{
    private static HashSet<uint>? ArmoireAvailableItems;

    private static AtkUnitBase* AddonMiragePrismPrismBox =>
        (AtkUnitBase*)Service.Gui.GetAddonByName("MiragePrismPrismBox");

    public override void Init()
    {
        ArmoireAvailableItems ??= LuminaCache.Get<Cabinet>()
                                             .Where(x => x.Item.Row > 0)
                                             .Select(x => x.Item.Row)
                                             .ToHashSet();

        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = false };
    }

    public override void ConfigUI()
    {
        ImGui.BeginDisabled(TaskManager.IsBusy);
        if (ImGui.Button(Service.Lang.GetText("Start")))
        {
            if (AddonMiragePrismPrismBox == null) return;
            var instance = MirageManager.Instance();
            for (var i = 0U; i < 800U; i++)
            {
                var item = instance->PrismBoxItemIds[i];
                if (item == 0) continue;
                var itemID = item > 100000 ? item % 100000 : item;
                if (ArmoireAvailableItems.Contains(itemID))
                {
                    var index = i;
                    TaskManager.Enqueue(() => instance->RestorePrismBoxItem(index));
                    TaskManager.DelayNext(100);
                }
            }
        }

        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("Stop"))) TaskManager.Abort();
    }
}
