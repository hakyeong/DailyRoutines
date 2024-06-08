using System.Numerics;
using DailyRoutines.Helpers;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using DailyRoutines.Windows;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.Colors;
using Dalamud.Memory;

using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoMaterializeTitle", "AutoMaterializeDescription", ModuleCategories.界面操作)]
public class AutoMaterialize : DailyModuleBase
{
    public override void Init()
    {
        TaskHelper ??= new TaskHelper { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = false };
        Overlay ??= new Overlay(this);
        Overlay.Flags |= ImGuiWindowFlags.NoMove;

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "Materialize", OnAddon);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "Materialize", OnAddon);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "MaterializeDialog", OnDialogAddon);
    }

    public override unsafe void OverlayUI()
    {
        var addon = (AtkUnitBase*)Service.Gui.GetAddonByName("Materialize");
        if (addon == null) return;

        var pos = new Vector2(addon->GetX() + 6, addon->GetY() - ImGui.GetWindowSize().Y + 6);
        ImGui.SetWindowPos(pos);

        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(ImGuiColors.DalamudYellow, Service.Lang.GetText("AutoMaterializeTitle"));

        ImGui.SameLine();
        ImGui.BeginDisabled(TaskHelper.IsBusy);
        if (ImGui.Button(Service.Lang.GetText("Start"))) TaskHelper.Enqueue(StartARound);
        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("Stop"))) TaskHelper.Abort();
    }

    private void OnAddon(AddonEvent type, AddonArgs args)
    {
        Overlay.IsOpen = type switch
        {
            AddonEvent.PostSetup => true,
            AddonEvent.PreFinalize => false,
            _ => Overlay.IsOpen,
        };
    }

    private static unsafe void OnDialogAddon(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)args.Addon;
        if (addon == null) return;

        AddonHelper.Callback(addon, true, 0);
    }

    private unsafe bool? StartARound()
    {
        if (!Throttler.Throttle("AutoMaterialize")) return false;
        if (Service.Condition[ConditionFlag.Mounted])
        {
            TaskHelper.Abort();
            return true;
        }

        if (Flags.OccupiedInEvent) return false;
        if (!TryGetAddonByName<AtkUnitBase>("Materialize", out var addon) || !IsAddonAndNodesReady(addon)) return false;

        var firstItemData = MemoryHelper.ReadStringNullTerminated((nint)addon->AtkValues[3].String);
        if (string.IsNullOrEmpty(firstItemData))
        {
            TaskHelper.Abort();
            return true;
        }

        var parts = firstItemData.Split(',');
        if (parts.Length == 0)
        {
            TaskHelper.Abort();
            return true;
        }

        foreach (var part in parts)
            if (part == "100%")
            {
                var agent = AgentModule.Instance()->GetAgentByInternalId(AgentId.Materialize);
                if (agent == null) return false;
                AgentHelper.SendEvent(agent, 0, 2, 0);

                TaskHelper.DelayNext(1500);
                TaskHelper.Enqueue(StartARound);
                return true;
            }

        TaskHelper.Abort();
        return true;
    }

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddon);
        Service.AddonLifecycle.UnregisterListener(OnDialogAddon);

        base.Uninit();
    }
}
