using System.Collections.Generic;
using System.Linq;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Hooking;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoPreventDuplicateStatusTitle", "AutoPreventDuplicateStatusDescription", ModuleCategories.Combat)]
public unsafe class AutoPreventDuplicateStatus : DailyModuleBase
{
    private enum DetectType
    {
        Self = 0,
        // Member = 1,
        Target = 2
    }

    private delegate bool UseActionSelfDelegate(
        ActionManager* actionManager, uint actionType, uint actionID, ulong targetID = 0xE000_0000, uint a4 = 0,
        uint a5 = 0, uint a6 = 0, void* a7 = null);

    private Hook<UseActionSelfDelegate>? useActionSelfHook;

    // Action ID - Status ID - DetectType
    private static readonly Dictionary<uint, (uint[] StatusID, DetectType DetectType)> DuplicateActions = new()
    {
        // 雪仇
        { 7535, ([1193], DetectType.Target) },
        // 昏乱
        { 7560, ([1203], DetectType.Target) },
        // 牵制
        { 7549, ([1195], DetectType.Target) },
        // 武装解除
        { 2887, ([860], DetectType.Target) },
        // 策动, 行吟, 防守之桑巴
        { 16889, ([1951, 1934, 1826], DetectType.Self) },
        { 7405, ([1951, 1934, 1826], DetectType.Self) },
        { 16012, ([1951, 1934, 1826], DetectType.Self) },
        // 扫腿, 下踢, 盾牌猛击
        { 7863, ([2], DetectType.Target) },
        { 7540, ([2], DetectType.Target) },
        { 16, ([2], DetectType.Target) },
        // 摆脱
        { 7388, ([1457], DetectType.Self) },
        // 大地神的抒情恋歌
        { 7408, ([1202], DetectType.Self) },
        // 战斗连祷
        { 3557, ([786], DetectType.Self) },
        // 夺取
        { 2248, ([638], DetectType.Target) },
        // 神秘环
        { 24405, ([2599], DetectType.Self) },
        // 义结金兰
        { 7396, ([1182, 1185], DetectType.Self) },
        // 灼热之光
        { 25801, ([2703], DetectType.Self) },
        // 鼓励
        { 7520, ([1239], DetectType.Self) },
        // 技巧舞步
        { 15998, ([1822], DetectType.Self) },
        // 战斗之声
        { 118, ([141], DetectType.Self) },
        // 连环计
        { 7436, ([1221], DetectType.Target) },
        // 占卜
        { 16552, ([1878], DetectType.Self) },
        // 复活, 复生, 生辰, 复苏, 赤复活, 天使低语
        { 125, ([148], DetectType.Target) },
        { 173, ([148], DetectType.Target) },
        { 3603, ([148], DetectType.Target) },
        { 24287, ([148], DetectType.Target) },
        { 7523, ([148], DetectType.Target) },
        { 18317, ([148], DetectType.Target) },
        // 必灭之炎
        { 34579, ([3643], DetectType.Target)}
    };

    private static Dictionary<uint, bool> ConfigEnabledActions = [];

    public override void Init()
    {
        useActionSelfHook =
            Service.Hook.HookFromAddress<UseActionSelfDelegate>((nint)ActionManager.MemberFunctionPointers.UseAction,
                                                                UseActionSelf);
        useActionSelfHook?.Enable();

        AddConfig(this, "EnabledActions", ConfigEnabledActions);
        ConfigEnabledActions = GetConfig<Dictionary<uint, bool>>(this, "EnabledActions");

        DuplicateActions.Keys.ToList().ForEach(key => ConfigEnabledActions[key] = true);
        ConfigEnabledActions.Keys.Except(DuplicateActions.Keys).ToList()
                            .ForEach(key => ConfigEnabledActions.Remove(key));

        UpdateConfig(this, "EnabledActions", ConfigEnabledActions);
    }

    public override void ConfigUI()
    {
        if (ImGui.BeginCombo("###ActionEnabledCombo", $"已启用 {ConfigEnabledActions.Count(x => x.Value)} 个技能", ImGuiComboFlags.HeightLarge))
        {
            if (ImGui.BeginTable("###ActionTable", 2, ImGuiTableFlags.Borders))
            {
                ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
                ImGui.TableNextColumn();
                ImGui.Text("技能");
                ImGui.TableNextColumn();
                ImGui.Text("关联状态");

                foreach (var tuple in DuplicateActions)
                {
                    if (!Service.PresetData.PlayerActions.TryGetValue(tuple.Key, out var result)) continue;
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    var isActionEnabled = ConfigEnabledActions[tuple.Key];
                    if (ImGui.Checkbox($"###Is{tuple.Key}Enabled", ref isActionEnabled))
                    {
                        ConfigEnabledActions[tuple.Key] = isActionEnabled;
                        UpdateConfig(this, "EnabledActions", ConfigEnabledActions);
                    }

                    ImGui.SameLine();
                    ImGui.Spacing();

                    ImGui.SameLine();
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 2.5f);
                    ImGui.Image(Service.Texture.GetIcon(result.Icon).ImGuiHandle, ImGuiHelpers.ScaledVector2(20f));

                    ImGui.SameLine();
                    ImGui.Text($"{result.Name.ExtractText()}");

                    ImGui.TableNextColumn();

                    ImGui.Spacing();

                    foreach (var status in tuple.Value.StatusID)
                    {
                        var statusResult = Service.PresetData.Statuses[status];
                        ImGui.SameLine();
                        ImGui.Image(Service.Texture.GetIcon(statusResult.Icon).ImGuiHandle, ImGuiHelpers.ScaledVector2(24f));

                        ImGuiOm.TooltipHover(statusResult.Name.ExtractText());
                    }
                }
                ImGui.EndTable();
            }

            ImGui.EndCombo();
        }
    }

    private bool UseActionSelf(
        ActionManager* actionManager, uint actionType, uint actionID, ulong targetID, uint a4, uint a5, uint a6,
        void* a7)
    {
        if (actionType != 1 || !DuplicateActions.TryGetValue(actionID, out var statusTuple) ||
            !ConfigEnabledActions[actionID])
            return useActionSelfHook.Original(actionManager, actionType, actionID, targetID, a4, a5, a6, a7);

        StatusManager* statusManager = null;
        switch (statusTuple.DetectType)
        {
            case DetectType.Self:
                statusManager = ((BattleChara*)Service.ClientState.LocalPlayer.Address)->GetStatusManager;
                break;
            case DetectType.Target:
                if (Service.Target.Target != null && Service.Target.Target.ObjectKind == ObjectKind.BattleNpc)
                    statusManager = ((BattleChara*)Service.Target.Target.Address)->GetStatusManager;
                break;
        }

        if (statusManager != null)
        {
            foreach (var status in statusTuple.StatusID)
            {
                var statusIndex = statusManager->GetStatusIndex(status);
                if (statusIndex != -1 && statusManager->StatusSpan[statusIndex].RemainingTime > 1.5)
                    return false;
            }
        }

        return useActionSelfHook.Original(actionManager, actionType, actionID, targetID, a4, a5, a6, a7);
    }
}
