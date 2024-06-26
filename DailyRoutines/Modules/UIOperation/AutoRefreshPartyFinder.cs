using System;
using System.Numerics;
using System.Timers;
using DailyRoutines.Helpers;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using DailyRoutines.Windows;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Timer = System.Timers.Timer;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoRefreshPartyFinderTitle", "AutoRefreshPartyFinderDescription", ModuleCategories.界面操作)]
public class AutoRefreshPartyFinder : DailyModuleBase
{
    private static int ConfigRefreshInterval = 10; // 秒
    private static bool ConfigOnlyInactive = true;

    private static Vector2 WindowPos = new(1000);

    private static Timer? PFRefreshTimer;
    private static int RefreshTimes;
    private static int Cooldown;

    public override void Init()
    {
        AddConfig("RefreshInterval", ConfigRefreshInterval);
        AddConfig("OnlyInactive", ConfigOnlyInactive);

        ConfigRefreshInterval = GetConfig<int>("RefreshInterval");
        ConfigOnlyInactive = GetConfig<bool>("OnlyInactive");

        Overlay ??= new Overlay(this);

        PFRefreshTimer ??= new Timer(1000);
        PFRefreshTimer.AutoReset = true;
        PFRefreshTimer.Elapsed += OnRefreshTimer;
        Cooldown = ConfigRefreshInterval;

        if (Service.Gui.GetAddonByName("LookingForGroup") != nint.Zero)
            OnAddonPF(AddonEvent.PostSetup, null);

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "LookingForGroup", OnAddonPF);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, "LookingForGroup", OnAddonPF);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "LookingForGroup", OnAddonPF);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "LookingForGroupDetail", OnAddonLFGD);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "LookingForGroupDetail", OnAddonLFGD);
    }

    public override unsafe void OverlayUI()
    {
        var addon = (AtkUnitBase*)Service.Gui.GetAddonByName("LookingForGroup");
        var refreshButton = addon->GetButtonNodeById(47)->AtkComponentBase.AtkResNode;
        if (addon == null || refreshButton == null) return;

        ImGui.SetWindowPos(WindowPos);

        ImGui.BeginGroup();
        ImGui.SetNextItemWidth(50f * GlobalFontScale);
        ImGui.InputInt("##RefreshIntervalInput", ref ConfigRefreshInterval, 0, 0);

        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            ConfigRefreshInterval = Math.Max(1, ConfigRefreshInterval);
            UpdateConfig("RefreshInterval", ConfigRefreshInterval);
            Cooldown = ConfigRefreshInterval;

            PFRefreshTimer.Stop();
            if (AddonState.LookingForGroup != null)
            {
                PFRefreshTimer.Start();
                Cooldown = ConfigRefreshInterval;
            }
        }

        ImGui.SameLine();
        ImGui.Text(Service.Lang.GetText("AutoRefreshPartyFinder-RefreshInterval", Cooldown));

        ImGui.SameLine();
        if (ImGui.Checkbox(Service.Lang.GetText("AutoRefreshPartyFinder-OnlyInactive"), ref ConfigOnlyInactive))
            UpdateConfig("OnlyInactive", ConfigOnlyInactive);

        ImGui.EndGroup();

        var contentSize = ImGui.GetItemRectSize();
        var framePadding = ImGui.GetStyle().FramePadding;

        WindowPos = new Vector2(refreshButton->ScreenX - contentSize.X - (4 * framePadding.X),
                                refreshButton->ScreenY - framePadding.Y);
    }

    private void OnAddonPF(AddonEvent type, AddonArgs? args)
    {
        switch (type)
        {
            case AddonEvent.PostSetup:
                Cooldown = ConfigRefreshInterval;
                RefreshTimes = 0;
                PFRefreshTimer.Restart();
                Overlay.IsOpen = true;
                break;
            case AddonEvent.PostRefresh:
                if (ConfigOnlyInactive)
                {
                    Cooldown = ConfigRefreshInterval;
                    PFRefreshTimer.Restart();
                }

                break;
            case AddonEvent.PreFinalize:
                PFRefreshTimer.Stop();
                Overlay.IsOpen = false;
                break;
        }
    }

    private static void OnAddonLFGD(AddonEvent type, AddonArgs? args)
    {
        switch (type)
        {
            case AddonEvent.PostSetup:
                PFRefreshTimer.Stop();
                break;
            case AddonEvent.PreFinalize:
                Cooldown = ConfigRefreshInterval;
                PFRefreshTimer.Restart();
                break;
        }
    }

    private static unsafe void OnRefreshTimer(object? sender, ElapsedEventArgs e)
    {
        if (Cooldown > 1)
        {
            Cooldown--;
            return;
        }

        Cooldown = ConfigRefreshInterval;
        if (AddonState.LookingForGroup != null && IsAddonAndNodesReady(AddonState.LookingForGroup))
        {
            AgentHelper.SendEvent(AgentId.LookingForGroup, 1, 17);
            RefreshTimes++;

            if (RefreshTimes > 10)
            {
                AddonState.LookingForGroup->Close(true);
                Service.Framework.RunOnTick(() =>
                {
                    var agent = AgentModule.Instance()->GetAgentByInternalId(AgentId.LookingForGroup);
                    if (agent == null) return;
                    agent->Show();
                }, TimeSpan.FromMilliseconds(100));
            }
            return;
        }

        PFRefreshTimer.Stop();
    }

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddonPF);

        if (PFRefreshTimer != null)
        {
            PFRefreshTimer.Elapsed -= OnRefreshTimer;
            PFRefreshTimer.Stop();
        }

        PFRefreshTimer?.Dispose();
        PFRefreshTimer = null;

        base.Uninit();
    }
}
