using System.Collections.Generic;
using ClickLib;
using ClickLib.Clicks;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.AddonLifecycle;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Memory;
using ECommons.Automation;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoRetainerCollectTitle", "AutoRetainerCollectDescription", ModuleCategories.Retainer)]
public class AutoRetainerCollect : IDailyModule
{
    public bool Initialized { get; set; }

    private static TaskManager? TaskManager;

    private static bool IsOnProcess;

    public void UI() { }

    public void Init()
    {
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "RetainerList", OnRetainerList);
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = false };

        Initialized = true;
    }

    private static unsafe void OnRetainerList(AddonEvent type, AddonArgs args)
    {
        if (!IsOnProcess)
        {
            IsOnProcess = true;

            var retainerManager = RetainerManager.Instance();
            var serverTime = Framework.GetServerTime();
            var completeRetainers = new List<int>();
            for (var i = 0; i < 10; i++)
            {
                var retainerState = retainerManager->GetRetainerBySortedIndex((uint)i)->VentureComplete;
                if (retainerState == 0) continue;
                if (retainerState - serverTime <= 0) completeRetainers.Add(i);
            }

            foreach (var index in completeRetainers) EnqueueSingleRetainer(index, completeRetainers.Count);

            IsOnProcess = false;
        }
    }

    private static void EnqueueSingleRetainer(int index, int completeRetainerCount)
    {
        // 点击指定雇员
        TaskManager.Enqueue(() => ClickSpecificRetainer(index));
        // 点击查看探险情况
        TaskManager.Enqueue(CheckVentureState);
        // 重新派遣
        TaskManager.Enqueue(ClickVentureReassign);
        // 确认派遣
        TaskManager.Enqueue(ClickVentureConfirm);
        // 回到雇员列表
        TaskManager.Enqueue(ExitToRetainerList);
    }

    private static unsafe bool? ClickSpecificRetainer(int index)
    {
        if (TryGetAddonByName<AddonRetainerList>("RetainerList", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(&addon->AtkUnitBase))
        {
            var handler = new ClickRetainerList();
            handler.Retainer(index);
            return true;
        }

        return false;
    }

    internal static unsafe bool? CheckVentureState()
    {
        if (TryGetAddonByName<AtkUnitBase>("SelectString", out var addon) && IsAddonReady(addon))
        {
            var text = MemoryHelper
                       .ReadSeString(
                           &addon->UldManager.NodeList[2]->GetAsAtkComponentNode()->Component->UldManager.NodeList[6]->
                               GetAsAtkComponentNode()->Component->UldManager.NodeList[3]->GetAsAtkTextNode()->NodeText)
                       .ExtractText().Trim();
            Service.Log.Debug(text);
            if (string.IsNullOrEmpty(text) || text.Contains('～'))
            {
                TaskManager?.Abort();
                TaskManager.Enqueue(ExitToRetainerList);
                IsOnProcess = false;
                return false;
            }

            if (Click.TrySendClick("select_string6")) return true;
        }

        return false;
    }

    private static unsafe bool? ClickVentureReassign()
    {
        if (TryGetAddonByName<AddonRetainerTaskResult>("RetainerTaskResult", out var addon) &&
            IsAddonReady(&addon->AtkUnitBase))
        {
            if (Click.TrySendClick("retainer_venture_result_reassign"))
                return true;
        }

        return false;
    }

    private static unsafe bool? ClickVentureConfirm()
    {
        if (TryGetAddonByName<AddonRetainerTaskAsk>("RetainerTaskAsk", out var addon) &&
            IsAddonReady(&addon->AtkUnitBase))
        {
            if (Click.TrySendClick("retainer_venture_ask_assign"))
                return true;
        }

        return false;
    }

    private static unsafe bool? ExitToRetainerList()
    {
        if (TryGetAddonByName<AtkUnitBase>("SelectString", out var addon) && IsAddonReady(addon))
        {
            if (Click.TrySendClick("select_string13"))
                return true;
        }

        return false;
    }

    public void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnRetainerList);
        IsOnProcess = false;
        TaskManager?.Abort();

        Initialized = false;
    }
}
