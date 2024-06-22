using System;
using DailyRoutines.Managers;
using Dalamud.Hooking;
using Dalamud.Interface.Utility;
using Dalamud.Utility.Signatures;

using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("SoundEffectThrottlerTitle", "SoundEffectThrottlerDescription", ModuleCategories.系统)]
public class SoundEffectThrottler : DailyModuleBase
{
    [Signature("40 53 41 55 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? F6 05",
               DetourName = nameof(PlaySoundEffectDetour))]
    private static Hook<PlaySoundEffectDelegate>? PlaySoundEffectHook;

    private static Config? ModuleConfig;

    public override void Init()
    {
        ModuleConfig = LoadConfig<Config>() ?? new();

        Service.Hook.InitializeFromAttributes(this);
        PlaySoundEffectHook?.Enable();
    }

    public override void ConfigUI()
    {
        ImGui.SetNextItemWidth(100f * GlobalFontScale);
        ImGui.InputInt(Service.Lang.GetText("SoundEffectThrottler-Throttle"), ref ModuleConfig.Throttle, 0, 0);
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            ModuleConfig.Throttle = Math.Max(100, ModuleConfig.Throttle);
            SaveConfig(ModuleConfig);
        }

        ImGuiOm.HelpMarker(Service.Lang.GetText("SoundEffectThrottler-ThrottleHelp", ModuleConfig.Throttle));

        ImGui.SetNextItemWidth(100f * GlobalFontScale);
        ImGui.SliderInt(Service.Lang.GetText("SoundEffectThrottler-Volume"), ref ModuleConfig.Volume, 1, 3);
        if (ImGui.IsItemDeactivatedAfterEdit())
            SaveConfig(ModuleConfig);
    }

    private static void PlaySoundEffectDetour(uint sound, nint a2, nint a3, byte a4)
    {
        var se = sound - 36;
        switch (se)
        {
            case <= 16 when Throttler.Throttle($"SoundEffectThorttler-{se}", ModuleConfig.Throttle):
                for (var i = 0; i < ModuleConfig.Volume; i++)
                    PlaySoundEffectHook.Original(sound, a2, a3, a4);

                break;
            case > 16:
                PlaySoundEffectHook.Original(sound, a2, a3, a4);
                break;
        }
    }

    private delegate void PlaySoundEffectDelegate(uint sound, nint a2, nint a3, byte a4);

    private class Config : ModuleConfiguration
    {
        public int Throttle = 1000;
        public int Volume = 3;
    }
}
