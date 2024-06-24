using System;
using System.Threading;
using DailyRoutines.Helpers;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Fate;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoFateSyncTitle", "AutoFateSyncDescription", ModuleCategories.战斗)]
public class AutoFateSync : DailyModuleBase
{
    private static readonly Throttler<string> Throttler = new();
    private static Config ModuleConfig = null!;

    private static CancellationTokenSource? CancelSource;

    public override void Init()
    {
        ModuleConfig = LoadConfig<Config>() ?? new();
        CancelSource ??= new();

        Service.ExecuteCommandManager.Register(OnExecuteCommand);
    }

    public override void ConfigUI()
    {
        ImGui.SetNextItemWidth(50f * GlobalFontScale);
        if (ImGui.InputFloat(Service.Lang.GetText("AutoFateSync-Delay"), ref ModuleConfig.Delay, 0, 0, "%.1f"))
            ModuleConfig.Delay = Math.Max(0, ModuleConfig.Delay);
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            SaveConfig(ModuleConfig);
            CancelSource.Cancel();
        }
        ImGuiOm.HelpMarker(Service.Lang.GetText("AutoFateSync-DelayHelp"));

        if (ImGui.Checkbox(Service.Lang.GetText("AutoFateSync-IgnoreMounting"), ref ModuleConfig.IgnoreMounting))
            SaveConfig(ModuleConfig);
        ImGuiOm.HelpMarker(Service.Lang.GetText("AutoFateSync-IgnoreMountingHelp"));
    }

    private static unsafe void OnExecuteCommand(int command, int param1, int param2, int param3, int param4)
    {
        if (command != 812) return;

        if (ModuleConfig.IgnoreMounting && (Service.Condition[ConditionFlag.InFlight] || Flags.IsOnMount))
        {
            Service.FrameworkManager.Register(OnFlying);
            return;
        }

        if (ModuleConfig.Delay > 0)
        {
            Service.Framework.RunOnTick(() =>
            {
                if (FateManager.Instance()->CurrentFate == null || Service.ClientState.LocalPlayer == null) return;

                Service.ExecuteCommandManager.ExecuteCommand(ExecuteCommandFlag.FateLevelSync, param1, 1);
            }, TimeSpan.FromSeconds(ModuleConfig.Delay), 0, CancelSource.Token);

            return;
        }

        Service.ExecuteCommandManager.ExecuteCommand(ExecuteCommandFlag.FateLevelSync, param1, 1);
    }

    private static unsafe void OnFlying(IFramework _)
    {
        if (!Throttler.Throttle("OnFlying")) return;

        var currentFate = FateManager.Instance()->CurrentFate;
        if (currentFate == null || Service.ClientState.LocalPlayer == null)
        {
            Service.FrameworkManager.Unregister(OnFlying);
            return;
        }

        if (Service.Condition[ConditionFlag.InFlight] || Flags.IsOnMount) return;

        Service.ExecuteCommandManager.ExecuteCommand(ExecuteCommandFlag.FateLevelSync, currentFate->FateId, 1);
        Service.FrameworkManager.Unregister(OnFlying);
    }

    public override void Uninit()
    {
        Service.ExecuteCommandManager.Unregister(OnExecuteCommand);
        CancelSource?.Cancel();
        CancelSource?.Dispose();
        CancelSource = null;

        base.Uninit();
    }

    private class Config : ModuleConfiguration
    {
        public bool IgnoreMounting = true;
        public float Delay = 3f;
    }
}
