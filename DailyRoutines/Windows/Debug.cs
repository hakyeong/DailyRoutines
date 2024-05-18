using System;
using DailyRoutines.Managers;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace DailyRoutines.Windows;

public class Debug() : Window("Daily Routines - 调试窗口", ImGuiWindowFlags.AlwaysAutoResize), IDisposable
{
    internal class Config
    {
        public bool ShowExecuteCommandLog;
        public bool ShowLogMessageLog;
        public bool ShowUseActionLog;
        public bool ShowUseActionLocationLog;
    }

    internal static Config DebugConfig = new();
    private static (int commmand, int p1, int p2, int p3, int p4) ExecuteCommandManual = new();

    public override void Draw()
    {
        ImGui.PushID("Execute Command Manager");
        ImGui.TextColored(ImGuiColors.DalamudYellow, "Execute Command Manager");
        ImGui.Separator();

        ImGui.Checkbox("显示日志###Execute Command Manager", ref DebugConfig.ShowExecuteCommandLog);

        ImGui.PushItemWidth(70f * ImGuiHelpers.GlobalScale);
        ImGui.InputInt("Command", ref ExecuteCommandManual.commmand, 0, 0);

        ImGui.InputInt("P1", ref ExecuteCommandManual.p1, 0, 0);
        ImGui.SameLine();
        ImGui.InputInt("P2", ref ExecuteCommandManual.p2, 0, 0);

        ImGui.InputInt("P3", ref ExecuteCommandManual.p3, 0, 0);
        ImGui.SameLine();
        ImGui.InputInt("P4", ref ExecuteCommandManual.p4, 0, 0);
        ImGui.PopItemWidth();

        ImGui.SameLine();
        if (ImGui.Button("执行"))
            Service.ExecuteCommandManager.ExecuteCommand(ExecuteCommandManual.commmand, ExecuteCommandManual.p1,
                                                         ExecuteCommandManual.p2, ExecuteCommandManual.p3,
                                                         ExecuteCommandManual.p4);
        ImGui.PopID();

        ImGui.TextColored(ImGuiColors.DalamudYellow, "Log Message Manager");
        ImGui.Separator();

        ImGui.Checkbox("显示日志###Log Message Manager", ref DebugConfig.ShowLogMessageLog);

        ImGui.TextColored(ImGuiColors.DalamudYellow, "Use Action Manager");
        ImGui.Separator();

        ImGui.Checkbox("显示 Action 日志###Use Action Manager", ref DebugConfig.ShowUseActionLog);
        ImGui.Checkbox("显示 Location Action 日志###Use Action Manager", ref DebugConfig.ShowUseActionLocationLog);
    }

    public void Dispose()
    {
        
    }
}
