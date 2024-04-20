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

        if (Service.WindowManager.WindowSystem.Windows.Any(x => x.WindowName == WindowName))
            Service.WindowManager.WindowSystem.RemoveWindow(Service.WindowManager.WindowSystem.Windows.FirstOrDefault(x => x.WindowName == WindowName));
        Service.WindowManager.WindowSystem.AddWindow(this);
    }

    public override void Draw()
    {
        ModuleBase.OverlayUI();
    }

    public override bool DrawConditions()
    {
        return ModuleBase.Initialized;
    }
}
