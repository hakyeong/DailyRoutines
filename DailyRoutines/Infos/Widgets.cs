using System;
using System.Numerics;
using DailyRoutines.Helpers;
using DailyRoutines.Managers;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
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
        var imageState = ImageHelper.TryGetImage(imageUrl, out var imageHandle);

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
            foreach (var world in PresetData.CNWorlds)
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
            ImGui.EndCombo();
        }

        if (ImGui.IsItemClicked())
        {
            ImGui.OpenPopup("###ContentSelectPopup");
        }

        ImGui.SetNextWindowSize(ImGuiHelpers.ScaledVector2(450f, 400f));
        if (ImGui.BeginPopup("###ContentSelectPopup", ImGuiWindowFlags.NoScrollbar))
        {
            ImGui.SetNextItemWidth(-1f);
            ImGui.InputTextWithHint
                ("###ContentSearchInput", Service.Lang.GetText("PleaseSearch"), ref contentSearchInput, 32);

            ImGui.Separator();

            var tableSize = new Vector2(ImGui.GetContentRegionAvail().X, 0);
            if (ImGui.BeginTable("###ContentSelectTable", 5, ImGuiTableFlags.Borders, tableSize))
            {
                ImGui.TableSetupColumn("RadioButton", ImGuiTableColumnFlags.WidthFixed, 20f * ImGuiHelpers.GlobalScale);
                ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthFixed, 20f * ImGuiHelpers.GlobalScale);
                ImGui.TableSetupColumn("Level", ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("123").X);
                ImGui.TableSetupColumn("DutyName", ImGuiTableColumnFlags.WidthStretch, 40);
                ImGui.TableSetupColumn("PlaceName", ImGuiTableColumnFlags.WidthStretch, 40);

                ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
                ImGui.TableNextColumn();
                ImGui.Text("");
                ImGui.TableNextColumn();
                ImGui.Text("类型");
                ImGui.TableNextColumn();
                ImGui.Text("等级");
                ImGui.TableNextColumn();
                ImGui.Text("副本名");
                ImGui.TableNextColumn();
                ImGui.Text("区域名");

                foreach (var contentPair in PresetData.Contents)
                {
                    var contentName = contentPair.Value.Name.RawString;
                    var placeName = contentPair.Value.TerritoryType.Value.PlaceName.Value.Name.RawString;
                    if (!string.IsNullOrWhiteSpace(contentSearchInput) &&
                        !contentName.Contains(contentSearchInput, StringComparison.OrdinalIgnoreCase) &&
                        !placeName.Contains(contentSearchInput, StringComparison.OrdinalIgnoreCase)) continue;

                    ImGui.PushID($"{contentName}_{contentPair.Key}");
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    ImGui.RadioButton("", selectedContent == contentPair.Value);

                    ImGui.TableNextColumn();
                    ImGui.Image(ImageHelper.GetIcon(contentPair.Value.ContentType.Value.Icon).ImGuiHandle, ImGuiHelpers.ScaledVector2(20f));

                    ImGui.TableNextColumn();
                    ImGui.Text(contentPair.Value.ClassJobLevelRequired.ToString());

                    ImGui.TableNextColumn();
                    if (ImGui.Selectable(contentName, false, 
                                         ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.DontClosePopups))
                    {
                        selectedContent = contentPair.Value;
                        selectState = true;
                    }

                    ImGui.TableNextColumn();
                    ImGui.Text(placeName);

                    if (ImGui.IsWindowAppearing() && selectedContent == contentPair.Value) ImGui.SetScrollHereY();
                    ImGui.PopID();
                }

                ImGui.EndTable();
            }
            ImGui.EndPopup();
        }

        return selectState;
    }

    public static void ConflictKeyText()
    {
        ImGui.Text($"{Service.Lang.GetText("ConflictKey")}: {Service.Config.ConflictKey}");
    }
}
