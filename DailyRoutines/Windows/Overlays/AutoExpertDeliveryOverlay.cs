using System;
using System.Numerics;
using DailyRoutines.Managers;
using DailyRoutines.Modules;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace DailyRoutines.Windows.Overlays;

public class AutoExpertDeliveryOverlay : Window, IDisposable
{
    public AutoExpertDeliveryOverlay() :
        base("Daily Routines - AutoExpertDeliveryOverlay",
             ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoMove)
    {
        ForceMainWindow = true;
    }

    public override unsafe void Draw()
    {
        var addon = (AtkUnitBase*)Service.Gui.GetAddonByName("GrandCompanySupplyList");
        if (addon == null) return;
        Position = new Vector2(addon->GetX() - ImGui.GetWindowSize().X, addon->GetY() + 6);
        ImGui.TextColored(ImGuiColors.DalamudYellow, Service.Lang.GetText("AutoExpertDeliveryTitle"));
        ImGui.PushTextWrapPos(300f * ImGuiHelpers.GlobalScale); // IDK WHY, BUT IT HAS TO BE SO LARGE
        ImGui.TextDisabled(Service.Lang.GetText("AutoExpertDeliveryDescription"));
        ImGui.PopTextWrapPos();

        ImGui.Separator();

        ImGui.BeginDisabled(AutoExpertDelivery.IsOnProcess);
        if (ImGui.Button(Service.Lang.GetText("AutoExpertDelivery-Start"))) AutoExpertDelivery.StartHandOver();
        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("AutoExpertDelivery-Stop"))) AutoExpertDelivery.EndHandOver();
    }

    public void Dispose() { }
}
