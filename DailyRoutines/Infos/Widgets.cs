using System.Numerics;
using DailyRoutines.Managers;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;

namespace DailyRoutines.Infos;

public static class Widgets
{
    private static SeString? rPrefix;
    private static SeString? drPrefix;
    private static SeString? dailyRoutinesPrefix;

    public static SeString DRPrefix()
    {
        return drPrefix ??= new SeStringBuilder()
                           .AddUiForeground(SeIconChar.BoxedLetterD.ToIconString(), 34)
                           .AddUiForeground(SeIconChar.BoxedLetterR.ToIconString(), 34)
                           .AddUiForegroundOff().Build();
    }

    public static SeString DailyRoutinesPrefix()
    {
        return dailyRoutinesPrefix ??= new SeStringBuilder()
                            .AddUiForeground("[Daily Routines]", 34)
                            .AddUiForegroundOff().Build();
    }

    public static SeString RPrefix()
    {
        return rPrefix ??= new SeStringBuilder()
                           .AddUiForeground(SeIconChar.BoxedLetterR.ToIconString(), 34)
                           .AddUiForegroundOff().Build();
    }

    public static SeString RPrefix(string text)
    {
        if (rPrefix == null) RPrefix();

        return new SeStringBuilder().Append(rPrefix).Append(text).Build();
    }

    public static void PreviewImageWithHelpText(
        string helpText, string imageUrl, Vector2 imageSize = default,
        FontAwesomeIcon imageIcon = FontAwesomeIcon.InfoCircle)
    {
        var imageState = ImageManager.TryGetImage(imageUrl, out var imageHandle);

        ImGui.TextColored(ImGuiColors.DalamudOrange, helpText);

        ImGui.SameLine();
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.TextDisabled(imageIcon.ToIconString());
        ImGui.PopFont();

        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            if (imageState)
                ImGui.Image(imageHandle.ImGuiHandle, imageSize == default ? imageHandle.Size : imageSize);
            else
                ImGui.TextDisabled($"{Service.Lang.GetText("ImageLoading")}...");
            ImGui.EndTooltip();
        }
    }

    public static bool CNWorldSelectCombo(ref World? selectedWorld, ref string worldSearchInput)
    {
        var selectState = false;
        if (ImGui.BeginCombo("###WorldSelectCombo", selectedWorld == null ? "" : selectedWorld.Name.RawString, ImGuiComboFlags.HeightLarge))
        {
            ImGui.InputTextWithHint("###WorldSearchInput", Service.Lang.GetText("PleaseSearch"), ref worldSearchInput, 32);

            ImGui.Separator();
            foreach (var world in Service.PresetData.CNWorlds)
            {
                var worldName = world.Value.Name.RawString;
                var dcName = world.Value.DataCenter.Value.Name.RawString;
                if (!string.IsNullOrWhiteSpace(worldSearchInput) && !worldName.Contains(worldSearchInput) && !dcName.Contains(worldSearchInput)) continue;

                if (ImGui.Selectable($"[{dcName}] {worldName}", selectedWorld != null && selectedWorld.RowId == world.Key))
                {
                    selectedWorld = world.Value;
                    ImGui.CloseCurrentPopup();
                    selectState = true;
                }

                ImGui.Separator();
            }

            ImGui.EndCombo();
        }

        return selectState;
    }

    public static bool ContentSelectCombo(ref ContentFinderCondition? selectedContent, ref string contentSearchInput)
    {
        var selectState = false;
        if (ImGui.BeginCombo("###ContentSelectCombo", selectedContent == null ? "" : selectedContent.Name.RawString, ImGuiComboFlags.HeightLarge))
        {
            ImGui.InputTextWithHint("###ContentSearchInput", Service.Lang.GetText("PleaseSearch"), ref contentSearchInput, 32);

            ImGui.Separator();
            foreach (var content in Service.PresetData.Contents)
            {
                var contentName = content.Value.Name.RawString;
                var placeName = content.Value.TerritoryType.Value.PlaceName.Value.Name.RawString;

                if (!string.IsNullOrWhiteSpace(contentSearchInput) && 
                    !contentName.Contains(contentSearchInput) && !placeName.Contains(contentSearchInput)) continue;

                if (ImGui.Selectable($"{placeName} | {contentName}", selectedContent != null && selectedContent.RowId == content.Key))
                {
                    selectedContent = content.Value;
                    ImGui.CloseCurrentPopup();
                    selectState = true;
                }

                ImGui.Separator();
            }

            ImGui.EndCombo();
        }

        return selectState;
    }

    public static void ConflictKeyText()
    {
        ImGui.Text($"{Service.Lang.GetText("ConflictKey")}: {Service.Config.ConflictKey}");
    }
}
