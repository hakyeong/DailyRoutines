using DailyRoutines.Infos;
using DailyRoutines.Windows;
using ECommons.Automation;
using System.Linq;
using System.Numerics;
using DailyRoutines.Managers;
using Dalamud.Game.AddonLifecycle;
using Dalamud.Interface.Colors;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoMaterializeTitle", "AutoMaterializeDescription", ModuleCategories.Interface)]
public class AutoMaterialize : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithConfigUI => false;
    internal static Overlay? Overlay { get; private set; }

    private static TaskManager? TaskManager;

    public void Init()
    {
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = false };
        Overlay ??= new Overlay(this);
        Overlay.Flags |= ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoBackground;

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "Materialize", OnAddon);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "Materialize", OnAddon);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "MaterializeDialog", OnDialogAddon);
    }

    public void ConfigUI() { }

    public unsafe void OverlayUI()
    {
        var addon = (AtkUnitBase*)Service.Gui.GetAddonByName("Materialize");
        if (addon == null) return;

        var title = addon->GetNodeById(14)->GetComponent()->GetTextNodeById(3);
        var pos = new Vector2(title->ScreenX + title->Width + 5, title->ScreenY - 3);
        ImGui.SetWindowPos(pos);

        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(ImGuiColors.DalamudYellow, Service.Lang.GetText("AutoMaterializeTitle"));

        ImGui.SameLine();
        ImGui.BeginDisabled(TaskManager.IsBusy);
        if (ImGui.Button(Service.Lang.GetText("AutoMaterialize-Start")))
        {
            TaskManager.Enqueue(StartARound);
        }
        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("AutoMaterialize-Stop")))
        {
            TaskManager.Abort();
        }
    }

    private static void OnAddon(AddonEvent type, AddonArgs args)
    {
        switch (type)
        {
            case AddonEvent.PostSetup:
                Overlay.IsOpen = true;
                break;
            case AddonEvent.PreFinalize:
                Overlay.IsOpen = false;
                break;
        }
    }

    private static unsafe void OnDialogAddon(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)args.Addon;
        if (addon == null) return;

        AddonManager.Callback(addon, true, 0);
    }

    private static unsafe bool? StartARound()
    {
        if (IsOccupied()) return false;
        if (TryGetAddonByName<AtkUnitBase>("Materialize", out var addon) && HelpersOm.IsAddonAndNodesReady(addon))
        {
            var firstItemData = MemoryHelper.ReadStringNullTerminated((nint)addon->AtkValues[3].String);
            if (string.IsNullOrEmpty(firstItemData))
            {
                TaskManager.Abort();
                return true;
            }

            var parts = firstItemData.Split(',');
            if (parts.Length == 0)
            {
                TaskManager.Abort();
                return true;
            }

            foreach (var part in parts)
            {
                if (part == "100%")
                {
                    AddonManager.Callback(addon, true, 2, 0);
                    TaskManager.DelayNext(1500);
                    TaskManager.Enqueue(StartARound);
                    return true;
                }
            }

            TaskManager.Abort();
            return true;
        }

        return false;
    }

    public void Uninit()
    {
        TaskManager?.Abort();
        Service.AddonLifecycle.UnregisterListener(OnAddon);
        Service.AddonLifecycle.UnregisterListener(OnDialogAddon);

        if (P.WindowSystem.Windows.Contains(Overlay)) P.WindowSystem.RemoveWindow(Overlay);
        Overlay = null;
    }
}
