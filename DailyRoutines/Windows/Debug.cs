using System;
using System.Linq;
using System.Reflection;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using DailyRoutines.Modules;
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
        if (ImGui.Button("输出模块一览"))
        {
            var allModules = Assembly.GetExecutingAssembly().GetTypes()
                                     .Where(t => typeof(DailyModuleBase).IsAssignableFrom(t) &&
                                                 t is { IsClass: true, IsAbstract: false } &&
                                                 t.GetCustomAttribute<ModuleDescriptionAttribute>() != null)
                                     .Select(type => new
                                     {
                                         Title = Service.Lang.GetText(
                                             type.GetCustomAttribute<ModuleDescriptionAttribute>()?.TitleKey ??
                                             "DevModuleTitle"),
                                         Description = Service.Lang.GetText(
                                             type.GetCustomAttribute<ModuleDescriptionAttribute>()?.DescriptionKey ??
                                             "DevModuleDescription"),
                                         Category = type.GetCustomAttribute<ModuleDescriptionAttribute>()?.Category ??
                                                    ModuleCategories.一般,
                                     })
                                     .OrderBy(m => m.Category)
                                     .ToList();

            var groupedModules = allModules.GroupBy(m => m.Category);

            var markdown = "";
            foreach (var group in groupedModules)
            {
                markdown += $"## {group.Key}\n";
                markdown += "| 名称 | 描述 |\n";
                markdown += "|------|------|\n";

                foreach (var module in group)
                {
                    var formattedDescription = module.Description.Replace("\n", "<br>");
                    markdown += $"| {module.Title} | {formattedDescription} |\n";
                }

                markdown += "\n";
            }

            ImGui.SetClipboardText(markdown);
        }

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
