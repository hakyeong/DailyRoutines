using System;
using System.Collections.Generic;
using System.Numerics;
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
        // 昏乱
        { 7560, ([1203], DetectType.Target) },
        // 武装解除
        { 2887, ([860], DetectType.Target)},
        // 策动, 行吟, 防守之桑巴
        { 16889, ([1951, 1934, 1826], DetectType.Self)},
        { 7405, ([1951, 1934, 1826], DetectType.Self)},
        { 16012, ([1951, 1934, 1826], DetectType.Self)},
        // 灼热之光
        { 25801, ([2703], DetectType.Self)},
        // 鼓励
        { 7520, ([1239], DetectType.Self)},
        // 牵制
        { 7549, ([1195], DetectType.Target)},
        // 扫腿, 下踢
        { 7863, ([2], DetectType.Target)},
        { 7540, ([2], DetectType.Target)},
        // 连环计
        { 7436, ([1221], DetectType.Target)},
        // 占卜
        { 16552, ([1221], DetectType.Target)},
        // 雪仇
        { 7535, ([1193], DetectType.Target)}
    };

    private static Dictionary<uint, bool> ConfigEnabledActions = [];

    public override void Init()
    {
        useActionSelfHook =
            Service.Hook.HookFromAddress<UseActionSelfDelegate>((nint)ActionManager.MemberFunctionPointers.UseAction,
                                                                UseActionSelf);
        useActionSelfHook?.Enable();

        foreach (var action in DuplicateActions.Keys)
        {
            ConfigEnabledActions[action] = true;
        }

        AddConfig(this, "EnabledActions", ConfigEnabledActions);
        ConfigEnabledActions = GetConfig<Dictionary<uint, bool>>(this, "EnabledActions");
    }

    public override void ConfigUI()
    {
        if (ImGui.BeginTable("###ActionEnabledTable", 1, ImGuiTableFlags.Borders, new Vector2(Math.Max(ImGui.GetContentRegionMax().X / 2.5f, 180f * ImGuiHelpers.GlobalScale))))
        {
            foreach (var action in DuplicateActions.Keys)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                var isActionEnabled = ConfigEnabledActions[action];
                if (ImGui.Checkbox($"###Is{action}Enabled", ref isActionEnabled))
                {
                    ConfigEnabledActions[action] = isActionEnabled;
                    UpdateConfig(this, "EnabledActions", ConfigEnabledActions);
                }

                ImGui.SameLine();
                ImGui.Spacing();

                ImGui.SameLine();
                ImGui.Image(Service.Texture.GetIcon(Service.PresetData.PlayerActions[action].Icon).ImGuiHandle, ImGuiHelpers.ScaledVector2(20f));

                ImGui.SameLine();
                ImGui.Text(Service.PresetData.PlayerActions[action].Name.ExtractText());
            }

            ImGui.EndTable();
        }
    }

    private bool UseActionSelf(ActionManager* actionManager, uint actionType, uint actionID, ulong targetID, uint a4, uint a5, uint a6, void* a7)
    {
        if (actionType != 1 || !DuplicateActions.TryGetValue(actionID, out var statusTuple) || !ConfigEnabledActions[actionID])
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
                if (statusManager->HasStatus(status))
                    return false;
            }
        }

        return useActionSelfHook.Original(actionManager, actionType, actionID, targetID, a4, a5, a6, a7);
    }

}
