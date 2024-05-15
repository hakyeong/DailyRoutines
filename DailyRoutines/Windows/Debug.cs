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
        public bool ShowLogMessageLog;
    }

    internal static Config DebugConfig = new();

    public override void Draw()
    {
        ImGui.TextColored(ImGuiColors.DalamudYellow, "Execute Command Manager");
        ImGui.Separator();

        ImGui.Checkbox("显示日志###Execute Command Manager", ref DebugConfig.ShowExecuteCommandLog);

        ImGui.TextColored(ImGuiColors.DalamudYellow, "Log Message Manager");
        ImGui.Separator();

        ImGui.Checkbox("显示日志###Log Message Manager", ref DebugConfig.ShowLogMessageLog);
    }

    public void Dispose()
    {
        
    }
}
