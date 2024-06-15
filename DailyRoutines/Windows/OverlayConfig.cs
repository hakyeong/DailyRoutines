using System.Reflection;
using DailyRoutines.Managers;
using DailyRoutines.Modules;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace DailyRoutines.Windows;

public class OverlayConfig : Window
{
    private DailyModuleBase ModuleBase { get; init; }

    private const ImGuiWindowFlags WindowFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.AlwaysAutoResize;

    public OverlayConfig(DailyModuleBase moduleBase) : 
        base($"{Service.Lang.GetText(
            moduleBase.GetType().GetCustomAttribute<ModuleDescriptionAttribute>()?.TitleKey ??
            "DevModuleTitle")}###{moduleBase}")
    {
        Flags = WindowFlags;
        RespectCloseHotkey = false;

        ModuleBase = moduleBase;

        Service.WindowManager.AddWindows(this);
    }

    public override void Draw() => ModuleBase.ConfigUI();

    public override bool DrawConditions()
    {
        return ModuleBase.Initialized;
    }
}
