using System.Collections.Generic;
using ClickLib.Clicks;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.AddonLifecycle;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoRequestItemSubmitTitle", "AutoRequestItemSubmitDescription", ModuleCategories.Base)]
public class AutoRequestItemSubmit : IDailyModule
{
    public bool Initialized { get; set; }

    private static TaskManager? TaskManager;
    private static readonly List<int> SlotsFilled = [];

    public void Init()
    {
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "Request", OnAddonRequest);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, "Request", OnAddonRequest);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "Request", OnAddonRequest);
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = true };

        Initialized = true;
    }

    public void UI() { }

    private void OnAddonRequest(AddonEvent type, AddonArgs args)
    {
        switch (type)
        {
            case AddonEvent.PostSetup:
                Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "SelectYesno", OnAddonSelectYesno);
                break;
            case AddonEvent.PostDraw:
                ClickRequestIcon();
                break;
            case AddonEvent.PreFinalize:
                SlotsFilled.Clear();
                TaskManager.Abort();
                Service.AddonLifecycle.UnregisterListener(OnAddonSelectYesno);
                break;
        }
    }

    private static unsafe void OnAddonSelectYesno(AddonEvent type, AddonArgs args)
    {
        if (TryGetAddonByName<AddonSelectYesno>("SelectYesno", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(&addon->AtkUnitBase))
        {
            var text = addon->PromptText->NodeText.ExtractText();
            if (text != null && text.Contains("优质"))
            {
                var handler = new ClickSelectYesNo();
                handler.Yes();
            }
        }
    }

    private static unsafe void ClickRequestIcon()
    {
        if (EzThrottler.Throttle("AutoRequestItemSubmit", 100))
        {
            if (TryGetAddonByName<AddonRequest>("Request", out var addon))
            {
                for (var i = 1; i <= addon->EntryCount; i++)
                {
                    if (SlotsFilled.Contains(addon->EntryCount)) ClickHandOver();
                    if (SlotsFilled.Contains(i)) return;
                    var index = i;
                    TaskManager.DelayNext($"AutoRequestItemSubmit{index}", 10);
                    TaskManager.Enqueue(() => TryClickItem(addon, index));
                }
            }
            else
            {
                SlotsFilled.Clear();
                TaskManager.Abort();
            }
        }
    }

    private static unsafe bool? TryClickItem(AddonRequest* addon, int i)
    {
        if (SlotsFilled.Contains(i)) return true;

        var contextMenu = (AtkUnitBase*)Svc.GameGui.GetAddonByName("ContextIconMenu");

        if (contextMenu is null || !contextMenu->IsVisible)
        {
            var slot = i - 1;

            Callback.Fire(&addon->AtkUnitBase, false, 2, slot, 0, 0);

            return false;
        }

        Callback.Fire(contextMenu, false, 0, 0, 1021003, 0, 0);
        SlotsFilled.Add(i);
        return true;
    }

    private static unsafe void ClickHandOver()
    {
        if (TryGetAddonByName<AddonRequest>("Request", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(&addon->AtkUnitBase))
        {
            var handler = new ClickRequest();
            handler.HandOver();
        }
    }

    public void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddonRequest);
        Service.AddonLifecycle.UnregisterListener(OnAddonRequest);
        Service.AddonLifecycle.UnregisterListener(OnAddonRequest);
        Service.AddonLifecycle.UnregisterListener(OnAddonSelectYesno);
        TaskManager?.Abort();
        SlotsFilled.Clear();

        Initialized = false;
    }
}
