using System.Collections.Generic;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoLeaveDutyTitle", "AutoLeaveDutyDescription", ModuleCategories.战斗)]
public class AutoLeaveDuty : DailyModuleBase
{
    private class Config : ModuleConfiguration
    {
        public HashSet<uint> BlacklistContents = [];
        public bool ForceToLeave;
    }

    private static Config ModuleConfig = null!;
    private static string ContentSearchInput = string.Empty;

    public override void Init()
    {
        ModuleConfig = LoadConfig<Config>() ?? new();

        TaskHelper ??= new();

        Service.DutyState.DutyCompleted += OnDutyComplete;
    }

    public override void ConfigUI()
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(ImGuiColors.DalamudOrange, $"{Service.Lang.GetText("AutoLeaveDuty-ForceToLeave")}:");

        ImGui.SameLine();
        if (ImGui.Checkbox("###ForceToLeave", ref ModuleConfig.ForceToLeave))
            SaveConfig(ModuleConfig);

        if (ModuleConfig.ForceToLeave)
        {
            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.TankBlue, Service.Lang.GetText("AutoLeaveDuty-Note"));
        }

        ImGui.Spacing();

        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(ImGuiColors.DalamudOrange, $"{Service.Lang.GetText("AutoLeaveDuty-BlacklistContents")}:");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(250f * GlobalFontScale);
        if (ContentSelectCombo(ref ModuleConfig.BlacklistContents, ref ContentSearchInput))
            SaveConfig(ModuleConfig);

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("AutoLeaveDuty-AddCurrentHighEnd")))
        {
            ModuleConfig.BlacklistContents.UnionWith(PresetData.HighEndContents.Keys);
            SaveConfig(ModuleConfig);
        }
    }

    private void OnDutyComplete(object? sender, ushort zone)
    {
        if (ModuleConfig.BlacklistContents.Contains(zone)) return;
        if (!ModuleConfig.ForceToLeave)
        {
            TaskHelper.Enqueue(() =>
            {
                if (Service.Condition[ConditionFlag.InCombat]) return false;

                Service.ExecuteCommandManager.ExecuteCommand(ExecuteCommandFlag.LeaveDuty);
                return true;
            });
        }
        else
        {
            Service.ExecuteCommandManager.ExecuteCommand(ExecuteCommandFlag.LeaveDuty, Service.Condition[ConditionFlag.InCombat] ? 1 : 0);
        }
    }

    public override void Uninit()
    {
        Service.DutyState.DutyCompleted -= OnDutyComplete;

        base.Uninit();
    }
}
