using System;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace DailyRoutines.Windows;

public class Debug() : Window("Daily Routines - 调试窗口", ImGuiWindowFlags.AlwaysAutoResize), IDisposable
{
    internal class Config
    {
        public bool ShowExecuteCommandLog;
    }

    internal static Config DebugConfig = new();

    public override void Draw()
    {
        ImGui.TextColored(ImGuiColors.DalamudYellow, "ExecuteCommand Manager");
        ImGui.Separator();

        ImGui.Checkbox("显示日志###ExecuteCommandManager", ref DebugConfig.ShowExecuteCommandLog);
    }

    public void Dispose()
    {
        
    }
}
