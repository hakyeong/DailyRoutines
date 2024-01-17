using System;
using ClickLib;
using ClickLib.Bases;
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

    private static TaskManager TaskManager = null!;

    public void UI() { }

    public void Init()
    {
        TaskManager = new TaskManager { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = false };

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "PunchingMachine", OnAddonSetup);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "GoldSaucerReward", OnAddonGSR);

        Initialized = true;
    }

    private void OnAddonSetup(AddonEvent type, AddonArgs args)
    {
        EnqueueStartGame();
        EnqueuePlayGame();
    }

    private static void EnqueueStartGame()
    {
        TaskManager.Enqueue(WaitSelectStringAddon);
        TaskManager.Enqueue(() => Click.TrySendClick("select_string1"));
    }

    private static void EnqueuePlayGame()
    {
        TaskManager.Enqueue(WaitGameAddon);
        TaskManager.Enqueue(ClickGameButton);
    }

    private static unsafe bool? WaitSelectStringAddon()
    {
        return TryGetAddonByName<AddonSelectString>("SelectString", out var addon) && IsAddonReady(&addon->AtkUnitBase);
    }

    private static unsafe bool? WaitGameAddon()
    {
        var result = TryGetAddonByName<AtkUnitBase>("PunchingMachine", out var addon) && IsAddonReady(addon);
        addon->IsVisible = false;
        return result;
    }

    private static bool? ClickGameButton()
    {
        var handler = new ClickPunchingMachine();
        return handler.ClickButton();
    }

    private unsafe void OnAddonGSR(AddonEvent type, AddonArgs args)
    {
        var ui = (AtkUnitBase*)args.Addon;
        if (!HelpersOm.IsAddonAndNodesReady(ui)) return;

        ui->IsVisible = false;

        StartAnotherRound();
    }

    private unsafe void StartAnotherRound()
    {
        var machineTarget = Service.Target.PreviousTarget;
        var machine = machineTarget.DataId == 2005029 ? (GameObject*)machineTarget.Address : null;

        if (machine != null)
            TargetSystem.Instance()->InteractWithObject(machine);
    }

    public void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddonSetup);
        Service.AddonLifecycle.UnregisterListener(OnAddonGSR);
        TaskManager?.Abort();

        Initialized = false;
    }
}

public class ClickPunchingMachine(nint addon = default) : ClickBase<ClickPunchingMachine>("PunchingMachine", addon)
{
    public unsafe bool? ClickButton()
    {
        var ui = (AtkUnitBase*)AddonAddress;

        var button = ui->GetButtonNodeById(23);
        if (button == null || !button->IsEnabled) return false;

        FireCallback(11, 3, new Random().Next(1700, 1999));

        return true;
    }
}
