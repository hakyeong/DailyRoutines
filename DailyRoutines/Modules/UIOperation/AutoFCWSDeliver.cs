using System;
using System.Numerics;
using System.Runtime.InteropServices;
using ClickLib;
using DailyRoutines.Helpers;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using DailyRoutines.Windows;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Interface.Colors;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace DailyRoutines.Modules;

[PrecedingModule([typeof(AutoRequestItemSubmit)])]
[ModuleDescription("AutoFCWSDeliverTitle", "AutoFCWSDeliverDescription", ModuleCategories.界面操作)]
public unsafe class AutoFCWSDeliver : DailyModuleBase
{
    private static AtkUnitBase* SubmarinePartsMenu => (AtkUnitBase*)Service.Gui.GetAddonByName("SubmarinePartsMenu");

    public override void Init()
    {
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = false };
        Overlay ??= new Overlay(this);

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "SelectYesno", OnAddonYesno);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "SubmarinePartsMenu", OnAddonMenu);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "SubmarinePartsMenu", OnAddonMenu);
    }

    public override void OverlayUI()
    {
        if (SubmarinePartsMenu == null)
        {
            Overlay.IsOpen = false;
            TaskManager.Abort();
            return;
        }

        var pos = new Vector2(SubmarinePartsMenu->GetX() + 6,
                              SubmarinePartsMenu->GetY() - ImGui.GetWindowSize().Y + 6);
        ImGui.SetWindowPos(pos);

        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(ImGuiColors.DalamudYellow, Service.Lang.GetText("AutoFCWSDeliverTitle"));

        ImGui.SameLine();
        ImGui.BeginDisabled(TaskManager.IsBusy);
        if (ImGui.Button(Service.Lang.GetText("Start")))
            EnqueueSubmit();
        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("Stop")))
            TaskManager.Abort();
    }

    private void EnqueueSubmit()
    {
        if (SubmarinePartsMenu == null || !IsAddonAndNodesReady(SubmarinePartsMenu)) return;
        TaskManager.Abort();

        var itemAmount = SubmarinePartsMenu->AtkValues[11].UInt;
        if (itemAmount <= 0) return;

        var listComponent = SubmarinePartsMenu->GetNodeById(38)->GetComponent()->UldManager.NodeList;
        if (listComponent == null) return;

        var isSomeTasksEnqueued = false;
        for (var i = 0U; i < itemAmount; i++)
        {
            var node = listComponent[i];
            if (node == null || !node->NodeFlags.HasFlag(NodeFlags.Enabled)) continue;

            isSomeTasksEnqueued = true;
            var nodeIndex = i;
            TaskManager.Enqueue(() =>
            {
                if (SubmarinePartsMenu == null || !IsAddonAndNodesReady(SubmarinePartsMenu)) return false;
                AddonHelper.Callback(SubmarinePartsMenu, true, 0, nodeIndex, 3U);
                return true;
            });
            TaskManager.DelayNext(500);
        }
        if (isSomeTasksEnqueued) TaskManager.Enqueue(EnqueueSubmit);
    }

    private void OnAddonMenu(AddonEvent type, AddonArgs args)
    {
        Overlay.IsOpen = type switch
        {
            AddonEvent.PostSetup => true,
            AddonEvent.PreFinalize => true,
            _ => Overlay.IsOpen
        };
    }

    private static void OnAddonYesno(AddonEvent type, AddonArgs args)
    {
        if (!TryGetAddonByName<AtkUnitBase>("SelectYesno", out var addon)) return;

        var text = Marshal.PtrToStringUTF8((nint)addon->AtkValues[0].String);
        if (string.IsNullOrWhiteSpace(text)) return;
        if (!text.Contains("确定要为合建设备提供")) return;

        if (text.Contains('0', StringComparison.OrdinalIgnoreCase))
        {
            Click.SendClick("select_no");
            return;
        }

        Click.SendClick("select_yes");
    }

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddonYesno);
        Service.AddonLifecycle.UnregisterListener(OnAddonMenu);

        base.Uninit();
    }
}
