using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using DailyRoutines.Helpers;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using DailyRoutines.Windows;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Internal;
using Dalamud.Interface.Internal.Notifications;
using Dalamud.Interface.Utility;
using Dalamud.Utility;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoShowDutyGuideTitle", "AutoShowDutyGuideDescription", ModuleCategories.战斗)]
public class AutoShowDutyGuide : DailyModuleBase
{
    private class Config : ModuleConfiguration
    {
        public float FontScale = 1f;
    }

    private static Config ModuleConfig = null!;

    private static readonly HttpClient client = new();
    private const string FF14OrgLinkBase = "https://gh.atmoomen.top/novice-network/master/docs/duty/{0}.md";
    private static uint CurrentDuty;
    private static IDalamudTextureWrap? NoviceIcon;

    private static List<string> GuideText = [];

    private static bool IsOnDebug;

    public override void Init()
    {
        ModuleConfig = LoadConfig<Config>() ?? new();

        NoviceIcon ??= ImageHelper.GetIcon(61523);

        Overlay ??= new Overlay(this);
        Overlay.Flags &= ~ImGuiWindowFlags.NoTitleBar;
        Overlay.Flags &= ~ImGuiWindowFlags.AlwaysAutoResize;
        Overlay.Flags |= ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoNavInputs;
        Overlay.ShowCloseButton = false;

        Service.ClientState.TerritoryChanged += OnZoneChange;
        if (Flags.BoundByDuty())
            OnZoneChange(Service.ClientState.TerritoryType);
    }

    public override void ConfigUI()
    {
        ImGui.TextColored(ImGuiColors.DalamudOrange, $"{Service.Lang.GetText("WorkTheory")}:");
        ImGuiOm.HelpMarker(Service.Lang.GetText("AutoShowDutyGuide-TheoryHelp"), 30f);

        ImGui.SetNextItemWidth(80f * ImGuiHelpers.GlobalScale);
        ImGui.InputFloat(Service.Lang.GetText("AutoShowDutyGuide-FontScale"), ref ModuleConfig.FontScale);
        if (ImGui.IsItemDeactivatedAfterEdit())
            SaveConfig(ModuleConfig);

        ImGui.BeginDisabled(Flags.BoundByDuty());
        if (ImGui.Checkbox(Service.Lang.GetText("AutoShowDutyGuide-DebugMode"), ref IsOnDebug))
        {
            if (IsOnDebug) OnZoneChange(172);
            else
            {
                GuideText.Clear();
                CurrentDuty = 0;
            }
        }
        ImGui.EndDisabled();

        ImGuiOm.TooltipHover(Service.Lang.GetText("AutoShowDutyGuide-DebugModeHelp"));
    }

    public override void OverlayOnOpen() => ImGui.SetScrollHereY();

    public override void OverlayUI()
    {
        if (!IsOnDebug && (!Flags.BoundByDuty() || GuideText.Count <= 0))
        {
            Overlay.IsOpen = false;
            GuideText.Clear();
            return;
        }

        if (GuideText.Count > 0)
            Overlay.WindowName = $"{GuideText[0]}###AutoShowDutyGuide-GuideWindow";

        PresetFont.Axis14.Push();
        if (ImGuiOm.SelectableImageWithText(NoviceIcon.ImGuiHandle, ImGuiHelpers.ScaledVector2(24f),
                                            Service.Lang.GetText("AutoShowDutyGuide-Source"), false))
            Util.OpenLink($"https://ff14.org/duty/{CurrentDuty}.htm");
        ImGui.Separator();

        ImGui.PushTextWrapPos(ImGui.GetWindowWidth());
        ImGui.SetWindowFontScale(ModuleConfig.FontScale);
        for (var i = 1; i < GuideText.Count; i++)
        {
            var text = GuideText[i];
            ImGui.PushID($"DutyGuideLine-{i}");
            ImGui.Text(text);
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            {
                ImGui.SetClipboardText(text);
                Service.DalamudNotice.AddNotification(new()
                {
                    Title = "Daily Routines",
                    Content = Service.Lang.GetText("AutoShowDutyGuide-CopyNotice"),
                    Type = NotificationType.Success,
                    InitialDuration = TimeSpan.FromSeconds(3),
                    ExtensionDurationSinceLastInterest = TimeSpan.FromSeconds(1)
                });
            }
            ImGui.PopID();

            ImGui.NewLine();
        }
        ImGui.SetWindowFontScale(1f);
        ImGui.PopTextWrapPos();
        PresetFont.Axis14.Pop();
    }

    private void OnZoneChange(ushort territory)
    {
        if (!PresetData.Contents.TryGetValue(territory, out var content))
        {
            CurrentDuty = 0;
            GuideText.Clear();
            Overlay.IsOpen = false;
            return;
        }
        Task.Run(async () => await GetDutyGuide(content.RowId));
    }

    private async Task GetDutyGuide(uint dutyID)
    {
        CurrentDuty = dutyID;
        var originalText = await client.GetStringAsync(string.Format(FF14OrgLinkBase, dutyID));

        var plainText = MarkdownToPlainText(originalText);
        if (!string.IsNullOrWhiteSpace(plainText))
        {
            GuideText = [.. plainText.Split('\n')];
            Overlay.IsOpen = true;
        }
    }

    public override void Uninit()
    {
        Service.ClientState.TerritoryChanged -= OnZoneChange;

        base.Uninit();
    }
}
