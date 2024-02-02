using System.Linq;
using DailyRoutines.Infos;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace DailyRoutines.Windows;

public class Overlay : Window
{
    private IDailyModule Module { get; init; }

    public Overlay(IDailyModule module) : base($"###{module}")
    {
        Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.AlwaysUseWindowPadding | ImGuiWindowFlags.NoTitleBar;
        RespectCloseHotkey = false;
        Module = module;

        if (P.WindowSystem.Windows.Any(x => x.WindowName == WindowName))
            P.WindowSystem.RemoveWindow(P.WindowSystem.Windows.FirstOrDefault(x => x.WindowName == WindowName));
        P.WindowSystem.AddWindow(this);
    }

    public override void Draw() => Module.OverlayUI();

    public override bool DrawConditions() => Module.Initialized;
}
