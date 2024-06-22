using System;
using ClickLib;
using DailyRoutines.Helpers;
using DailyRoutines.Infos;
using DailyRoutines.Infos.Clicks;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using GameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoCACTitle", "AutoCACDescription", ModuleCategories.金碟)]
public class AutoPunchingMachine : DailyModuleBase
{
    public override void Init()
    {
        TaskHelper ??= new TaskHelper();
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "PunchingMachine", OnAddonSetup);
    }

    public override void ConfigUI() { ConflictKeyText(); }

    private void OnAddonSetup(AddonEvent type, AddonArgs args)
    {
        if (InterruptByConflictKey()) return;

        TaskHelper.Enqueue(WaitSelectStringAddon);
        TaskHelper.Enqueue(ClickGameButton);
    }

    private unsafe bool? WaitSelectStringAddon()
    {
        if (InterruptByConflictKey()) return true;

        return TryGetAddonByName<AddonSelectString>("SelectString", out var addon) &&
               IsAddonAndNodesReady(&addon->AtkUnitBase) && Click.TrySendClick("select_string1");
    }

    private unsafe bool? ClickGameButton()
    {
        if (InterruptByConflictKey()) return true;

        if (!TryGetAddonByName<AtkUnitBase>("PunchingMachine", out var addon) || !IsAddonAndNodesReady(addon))
            return false;

        var button = addon->GetButtonNodeById(23);
        if (button == null || !button->IsEnabled) return false;

        addon->IsVisible = false;

        ClickPunchingMachine.Using((nint)addon).Play(new Random().Next(1700, 1999));

        TaskHelper.Enqueue(StartAnotherRound);
        return true;
    }

    private unsafe bool? StartAnotherRound()
    {
        if (InterruptByConflictKey()) return true;

        if (Flags.OccupiedInEvent) return false;
        var machineTarget = Service.Target.PreviousTarget;
        var machine = machineTarget.Name.TextValue.Contains("重击伽美什") ? (GameObject*)machineTarget.Address : null;

        if (machine != null)
        {
            TargetSystem.Instance()->InteractWithObject(machine);
            return true;
        }

        return false;
    }

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddonSetup);

        base.Uninit();
    }
}
