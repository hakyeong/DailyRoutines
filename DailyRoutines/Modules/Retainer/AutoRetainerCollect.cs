using System;
using ClickLib;
using ClickLib.Clicks;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using TaskManager = ECommons.Automation.TaskManager;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoRetainerCollectTitle", "AutoRetainerCollectDescription", ModuleCategories.Retainer)]
public unsafe class AutoRetainerCollect : DailyModuleBase
{
    public override void Init()
    {
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "RetainerList", OnRetainerList);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "RetainerList", OnRetainerList);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, "RetainerList", OnRetainerList);
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = false };
    }

    public override void ConfigUI()
    {
        ConflictKeyText();
    }

    private void OnRetainerList(AddonEvent type, AddonArgs args)
    {
        switch (type)
        {
            case AddonEvent.PostSetup:
                if (InterruptByConflictKey()) return;
                CheckAndEnqueueCollects();

                Service.AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, "RetainerList", OnRetainerList);
                break;
            case AddonEvent.PostUpdate:
                if (EzThrottler.Throttle("AutoRetainerCollect-AFK", 5000))
                {
                    if (InterruptByConflictKey() || TaskManager.IsBusy ||
                        !IsAddonAndNodesReady((AtkUnitBase*)args.Addon)) return;
                    Service.Framework.RunOnTick(CheckAndEnqueueCollects, TimeSpan.FromSeconds(1));
                }

                break;
            case AddonEvent.PreFinalize:
                Service.AddonLifecycle.UnregisterListener(AddonEvent.PostUpdate, "RetainerList", OnRetainerList);
                break;
        }

        return;

        void CheckAndEnqueueCollects()
        {
            var retainerManager = RetainerManager.Instance();
            var serverTime = Framework.GetServerTime();
            for (var i = 0; i < retainerManager->GetRetainerCount(); i++)
            {
                var retainerState = retainerManager->GetRetainerBySortedIndex((uint)i)->VentureComplete;
                if (retainerState == 0) continue;
                if (retainerState - serverTime <= 0)
                {
                    EnqueueSingleRetainer(i);
                    break;
                }
            }
        }
    }

    private void EnqueueSingleRetainer(int index)
    {
        if (InterruptByConflictKey()) return;

        TaskManager.Enqueue(() => ClickSpecificRetainer(index));
        TaskManager.Enqueue(CheckVentureState);
        TaskManager.Enqueue(() => Click.TrySendClick("retainer_venture_result_reassign"));
        TaskManager.Enqueue(() => Click.TrySendClick("retainer_venture_ask_assign"));
        TaskManager.Enqueue(ReturnToRetainerList);
    }

    private bool? ClickSpecificRetainer(int index)
    {
        if (InterruptByConflictKey()) return true;

        if (TryGetAddonByName<AddonRetainerList>("RetainerList", out var addon) &&
            IsAddonAndNodesReady(&addon->AtkUnitBase))
        {
            var handler = new ClickRetainerList();
            handler.Retainer(index);
            return true;
        }

        return false;
    }

    private bool? CheckVentureState()
    {
        if (InterruptByConflictKey()) return true;

        if (TryGetAddonByName<AtkUnitBase>("SelectString", out var addon) && IsAddonReady(addon))
        {
            if (!TryScanSelectStringText(addon, "返回", out var returnIndex) ||
                !TryScanSelectStringText(addon, "结束", out var index))
            {
                TaskManager.Abort();
                if (returnIndex != -1) TaskManager.Enqueue(() => Click.TrySendClick($"select_string{returnIndex + 1}"));

                return true;
            }

            if (Click.TrySendClick($"select_string{index + 1}")) return true;
        }

        return false;
    }

    private bool? ReturnToRetainerList()
    {
        if (InterruptByConflictKey()) return true;

        if (TryGetAddonByName<AtkUnitBase>("SelectString", out var addon) && IsAddonReady(addon))
        {
            if (!TryScanSelectStringText(addon, "返回", out var index))
            {
                TaskManager.Abort();
                return true;
            }

            if (Click.TrySendClick($"select_string{index + 1}")) return true;
        }

        return false;
    }

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnRetainerList);

        base.Uninit();
    }
}
