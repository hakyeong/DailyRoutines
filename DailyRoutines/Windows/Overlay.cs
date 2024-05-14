using System.Linq;
using DailyRoutines.Managers;
using DailyRoutines.Modules;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace DailyRoutines.Windows;

public class Overlay : Window
{
    private DailyModuleBase ModuleBase { get; init; }

    private const ImGuiWindowFlags WindowFlags =
        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar;

    public Overlay(DailyModuleBase moduleBase, string? title = null) : base(
        $"{(string.IsNullOrEmpty(title) ? string.Empty : title)}###{moduleBase}")
    {
        Flags = WindowFlags;
        RespectCloseHotkey = false;
        ModuleBase = moduleBase;

        Service.WindowManager.AddWindows(this);
    }

    public override void Draw() => ModuleBase.OverlayUI();

    public override void OnOpen() => ModuleBase.OverlayOnOpen();

    public override void OnClose() => ModuleBase.OverlayOnClose();

    public override void PreDraw() => ModuleBase.OverlayPreDraw();

    public override void PostDraw() => ModuleBase.OverlayPostDraw();

    public override void Update() => ModuleBase.OverlayUpdate();

    public override void PreOpenCheck() => ModuleBase.OverlayPreOpenCheck();

    public override bool DrawConditions()
    {
        return ModuleBase.Initialized;
    }
}
