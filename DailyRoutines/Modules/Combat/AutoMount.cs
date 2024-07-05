using DailyRoutines.Helpers;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.Colors;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Fate;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoMountTitle", "AutoMountDescription", ModuleCategories.战斗)]
public unsafe class AutoMount : DailyModuleBase
{
    private static Config ModuleConfig = null!;

    private static Mount? SelectedMount;
    private static string MountSearchInput = string.Empty;

    public override void Init()
    {
        ModuleConfig = LoadConfig<Config>() ?? new();
        if (ModuleConfig.SelectedMount != 0)
            SelectedMount = LuminaCache.GetRow<Mount>(ModuleConfig.SelectedMount);

        TaskHelper ??= new TaskHelper { AbortOnTimeout = true, TimeLimitMS = 20000, ShowDebug = false };

        Service.Condition.ConditionChange += OnConditionChanged;
        Service.ClientState.TerritoryChanged += OnZoneChanged;
    }

    public override void ConfigUI()
    {
        ImGui.TextColored(ImGuiColors.DalamudOrange, $"{Service.Lang.GetText("AutoMount-CurrentMount")}:");

        ImGui.SameLine();
        ImGui.Text(ModuleConfig.SelectedMount == 0
                       ? Service.Lang.GetText("AutoMount-RandomMount")
                       : LuminaCache.GetRow<Mount>(ModuleConfig.SelectedMount).Singular.RawString);

        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(ImGuiColors.DalamudOrange, $"{Service.Lang.GetText("AutoMount-SelecteMount")}:");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(150f * GlobalFontScale);
        if (MountSelectCombo(ref SelectedMount, ref MountSearchInput))
        {
            ModuleConfig.SelectedMount = SelectedMount.RowId;
            SaveConfig(ModuleConfig);
        }

        ImGui.SameLine();
        if (ImGui.SmallButton(Service.Lang.GetText("AutoMount-RandomMount")))
        {
            ModuleConfig.SelectedMount = 0;
            SaveConfig(ModuleConfig);
        }

        if (ImGui.Checkbox(Service.Lang.GetText("AutoMount-MountWhenZoneChange"), ref ModuleConfig.MountWhenZoneChange))
            SaveConfig(ModuleConfig);

        if (ImGui.Checkbox(Service.Lang.GetText("AutoMount-MountWhenGatherEnd"), ref ModuleConfig.MountWhenGatherEnd))
            SaveConfig(ModuleConfig);

        if (ImGui.Checkbox(Service.Lang.GetText("AutoMount-MountWhenCombatEnd"), ref ModuleConfig.MountWhenCombatEnd))
            SaveConfig(ModuleConfig);
    }

    private void OnZoneChanged(ushort zone)
    {
        if (!ModuleConfig.MountWhenZoneChange || zone == 0) return;
        if (!CanUseMountCurrentZone(zone)) return;

        TaskHelper.Abort();
        TaskHelper.DelayNext(500);
        TaskHelper.Enqueue(UseMount);
    }

    private void OnConditionChanged(ConditionFlag flag, bool value)
    {
        switch (flag)
        {
            case ConditionFlag.Gathering when !value && ModuleConfig.MountWhenGatherEnd:
            case ConditionFlag.InCombat when !value && ModuleConfig.MountWhenCombatEnd && !Service.ClientState.IsPvP &&
                                             (FateManager.Instance()->CurrentFate == null ||
                                              FateManager.Instance()->CurrentFate->Progress == 100):
                if (!CanUseMountCurrentZone()) return;

                TaskHelper.Abort();
                TaskHelper.DelayNext(500);
                TaskHelper.Enqueue(UseMount);
                break;
        }
    }

    private bool? UseMount()
    {
        if (!Throttler.Throttle("AutoMount")) return false;
        if (!(Service.ClientState.LocalPlayer?.IsTargetable ?? false)) return false;
        if (AgentMap.Instance()->IsPlayerMoving == 1) return true;
        if (Flags.IsCasting) return false;
        if (Flags.IsOnMount) return true;
        if (ActionManager.Instance()->GetActionStatus(ActionType.GeneralAction, 9) != 0) return false;

        TaskHelper.DelayNext(100);
        TaskHelper.Enqueue(() => ModuleConfig.SelectedMount == 0
                                     ? ActionManager.Instance()->UseAction(ActionType.GeneralAction, 9)
                                     : ActionManager.Instance()->UseAction(ActionType.Mount, ModuleConfig.SelectedMount));
        return true;
    }

    private static bool CanUseMountCurrentZone(ushort zone = 0)
    {
        if (zone == 0) zone = Service.ClientState.TerritoryType;
        if (zone == 0) return false;

        var zoneData = LuminaCache.GetRow<TerritoryType>(zone);
        return zoneData is { Mount: true };
    }

    public override void Uninit()
    {
        Service.ClientState.TerritoryChanged -= OnZoneChanged;
        Service.Condition.ConditionChange -= OnConditionChanged;

        base.Uninit();
    }

    private class Config : ModuleConfiguration
    {
        public bool MountWhenCombatEnd = true;
        public bool MountWhenGatherEnd = true;
        public bool MountWhenZoneChange = true;
        public uint SelectedMount;
    }
}
