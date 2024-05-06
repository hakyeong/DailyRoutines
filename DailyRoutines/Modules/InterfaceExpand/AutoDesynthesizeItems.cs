using System.Numerics;
using DailyRoutines.Helpers;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using DailyRoutines.Windows;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Interface.Colors;
using Dalamud.Memory;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoDesynthesizeItemsTitle", "AutoDesynthesizeItemsDescription", ModuleCategories.界面扩展)]
public unsafe class AutoDesynthesizeItems : DailyModuleBase
{
    private static bool ConfigSkipWhenHQ;

    public override void Init()
    {
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 10000, ShowDebug = false };
        Overlay ??= new Overlay(this);

        AddConfig("SkipWhenHQ", ConfigSkipWhenHQ);
        ConfigSkipWhenHQ = GetConfig<bool>("SkipWhenHQ");

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "SalvageItemSelector", OnAddonList);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "SalvageItemSelector", OnAddonList);
    }

    public override void OverlayUI()
    {
        var addon = (AtkUnitBase*)Service.Gui.GetAddonByName("SalvageItemSelector");
        if (addon == null) return;

        var pos = new Vector2(addon->GetX() - ImGui.GetWindowSize().X, addon->GetY() + 6);
        ImGui.SetWindowPos(pos);

        ImGui.TextColored(ImGuiColors.DalamudYellow, Service.Lang.GetText("AutoDesynthesizeItemsTitle"));

        ImGui.Separator();

        ImGui.BeginDisabled(TaskManager.IsBusy);
        if (ImGui.Checkbox(Service.Lang.GetText("AutoDesynthesizeItems-SkipHQ"), ref ConfigSkipWhenHQ))
            UpdateConfig("SkipWhenHQ", ConfigSkipWhenHQ);

        if (ImGui.Button(Service.Lang.GetText("Start"))) StartDesynthesize();
        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("Stop"))) TaskManager.Abort();
    }

    private void OnAddonList(AddonEvent type, AddonArgs args)
    {
        Overlay.IsOpen = type switch
        {
            AddonEvent.PostSetup => true,
            AddonEvent.PreFinalize => false,
            _ => Overlay.IsOpen
        };
    }

    private bool? StartDesynthesize()
    {
        if (IsOccupied()) return false;
        if (TryGetAddonByName<AtkUnitBase>("SalvageItemSelector", out var addon) &&
            IsAddonAndNodesReady(addon))
        {
            var itemAmount = addon->AtkValues[9].Int;
            if (itemAmount == 0)
            {
                TaskManager.Abort();
                return true;
            }

            for (var i = 0; i < itemAmount; i++)
            {
                var itemName = MemoryHelper.ReadStringNullTerminated((nint)addon->AtkValues[(i * 8) + 14].String);
                if (ConfigSkipWhenHQ)
                {
                    if (itemName.Contains('')) // HQ 符号
                        continue;
                }

                var agent = AgentModule.Instance()->GetAgentByInternalId(AgentId.Salvage);
                if (agent == null) return false;

                AgentHelper.SendEvent(agent, 0, 12, i);

                TaskManager.DelayNext(1500);
                TaskManager.Enqueue(StartDesynthesize);
                return true;
            }
        }

        return false;
    }

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddonList);

        base.Uninit();
    }
}
