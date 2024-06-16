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
    private static HashSet<uint> BlacklistContents = [];
    private static string ContentSearchInput = string.Empty;

    public override void Init()
    {
        AddConfig(nameof(BlacklistContents), BlacklistContents);
        BlacklistContents = GetConfig<HashSet<uint>>(nameof(BlacklistContents));

        Service.DutyState.DutyCompleted += OnDutyComplete;
    }

    public override void ConfigUI()
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(ImGuiColors.DalamudOrange, $"{Service.Lang.GetText("AutoLeaveDuty-BlacklistContents")}:");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(250f * ImGuiHelpers.GlobalScale);
        if (ContentSelectCombo(ref BlacklistContents, ref ContentSearchInput))
            UpdateConfig(nameof(BlacklistContents), BlacklistContents);

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("AutoLeaveDuty-AddCurrentHighEnd")))
        {
            BlacklistContents.UnionWith(PresetData.HighEndContents.Keys);
            UpdateConfig(nameof(BlacklistContents), BlacklistContents);
        }
    }

    private static void OnDutyComplete(object? sender, ushort zone)
    {
        if (BlacklistContents.Contains(zone)) return;

        Service.ExecuteCommandManager.ExecuteCommand(ExecuteCommandFlag.LeaveDuty, Service.Condition[ConditionFlag.InCombat] ? 1 : 0);
    }

    public override void Uninit()
    {
        Service.DutyState.DutyCompleted -= OnDutyComplete;
    }
}
