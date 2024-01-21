using System;
using ClickLib;
using DailyRoutines.Clicks;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.AddonLifecycle;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using GameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoCACTitle", "AutoCACDescription", ModuleCategories.GoldSaucer)]
public class AutoPunchingMachine : IDailyModule
{
    public bool Initialized { get; set; }

    private static TaskManager? TaskManager;

    public void UI() { }

    public void Init()
    {
        TaskManager = new TaskManager { AbortOnTimeout = true, TimeLimitMS = 10000, ShowDebug = false };

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "PunchingMachine", OnAddonSetup);

        Initialized = true;
    }

    private static void OnAddonSetup(AddonEvent type, AddonArgs args)
    {
        TaskManager.Enqueue(WaitSelectStringAddon);
        TaskManager.Enqueue(ClickGameButton);
    }

    private static unsafe bool? WaitSelectStringAddon()
    {
        if (TryGetAddonByName<AddonSelectString>("SelectString", out var addon) && IsAddonReady(&addon->AtkUnitBase))
            return Click.TrySendClick("select_string1");

        return false;
    }

    private static unsafe bool? ClickGameButton()
    {
        if (TryGetAddonByName<AtkUnitBase>("PunchingMachine", out var addon) && IsAddonReady(addon))
        {
            var button = addon->GetButtonNodeById(23);
            if (button == null || !button->IsEnabled) return false;

            addon->IsVisible = false;

            var handler = new ClickPunchingMachineDR();
            handler.Play(new Random().Next(1700, 1999));

            TaskManager.Enqueue(StartAnotherRound);
            return true;
        }

        return false;
    }

    private static unsafe bool? StartAnotherRound()
    {
        if (IsOccupied()) return false;
        var machineTarget = Service.Target.PreviousTarget;
        var machine = machineTarget.DataId == 2005029 ? (GameObject*)machineTarget.Address : null;

        if (machine != null)
        {
            TargetSystem.Instance()->InteractWithObject(machine);
            return true;
        }

        return false;
    }

    public void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddonSetup);
        TaskManager?.Abort();

        Initialized = false;
    }
}
