using System;
using System.Collections.Generic;
using System.Linq;
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
using Action = Lumina.Excel.GeneratedSheets.Action;

namespace DailyRoutines.Infos;

public static class Widgets
{
    public static SeString RPrefix => rPrefix.Value;
    public static SeString DRPrefix => drPrefix.Value;
    public static SeString DailyRoutinesPrefix => dailyRoutinesPrefix.Value;

    private static readonly Lazy<SeString> rPrefix = new(() =>
                                                             new SeStringBuilder()
                                                                 .AddUiForeground(
                                                                     SeIconChar.BoxedLetterR.ToIconString(), 34)
                                                                 .AddUiForegroundOff().Build());

    private static readonly Lazy<SeString> drPrefix = new(() =>
                                                              new SeStringBuilder()
                                                                  .AddUiForeground(
                                                                      SeIconChar.BoxedLetterD.ToIconString(), 34)
                                                                  .AddUiForeground(
                                                                      SeIconChar.BoxedLetterR.ToIconString(), 34)
                                                                  .AddUiForegroundOff().Build());

    private static readonly Lazy<SeString> dailyRoutinesPrefix = new(() =>
                                                                         new SeStringBuilder()
                                                                             .AddUiForeground("[Daily Routines]", 34)
                                                                             .AddUiForegroundOff().Build());

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
        if (ImGui.BeginCombo("###WorldSelectCombo", selectedWorld == null ? "" : selectedWorld.Name.RawString,
                             ImGuiComboFlags.HeightLarge))
        {
            ImGui.InputTextWithHint("###WorldSearchInput", Service.Lang.GetText("PleaseSearch"), ref worldSearchInput,
                                    32);

            ImGui.Separator();
            foreach (var world in PresetData.CNWorlds)
            {
                var worldName = world.Value.Name.RawString;
                var dcName = world.Value.DataCenter.Value.Name.RawString;
                if (!string.IsNullOrWhiteSpace(worldSearchInput) &&
                    !worldName.Contains(worldSearchInput) && !dcName.Contains(worldSearchInput))
                    continue;

                if (ImGui.Selectable($"[{dcName}] {worldName}",
                                     selectedWorld != null && selectedWorld.RowId == world.Key))
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
        if (ImGui.BeginCombo("###ContentSelectCombo", selectedContent == null ? "" : selectedContent.Name.RawString,
                             ImGuiComboFlags.HeightLarge))
            ImGui.EndCombo();

        if (ImGui.IsItemClicked())
            ImGui.OpenPopup("###ContentSelectPopup");

        ImGui.SetNextWindowSize(ImGuiHelpers.ScaledVector2(450f, 400f));
        if (ImGui.BeginPopup("###ContentSelectPopup"))
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
                    ImGui.Image(ImageHelper.GetIcon(contentPair.Value.ContentType.Value.Icon).ImGuiHandle,
                                ImGuiHelpers.ScaledVector2(20f));

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

                    ImGui.PopID();
                }

                ImGui.EndTable();
            }

            ImGui.EndPopup();
        }

        return selectState;
    }

    public static bool ContentSelectCombo(ref HashSet<uint> selected, ref string contentSearchInput)
    {
        var selectState = false;
        if (ImGui.BeginCombo("###ContentSelectCombo", $"当前已选中 {selected.Count} 个副本", ImGuiComboFlags.HeightLarge))
            ImGui.EndCombo();

        if (ImGui.IsItemClicked())
            ImGui.OpenPopup("###ContentSelectPopup");

        ImGui.SetNextWindowSize(ImGuiHelpers.ScaledVector2(450f, 400f));
        if (ImGui.BeginPopup("###ContentSelectPopup"))
        {
            ImGui.SetNextItemWidth(-1f);
            ImGui.InputTextWithHint
                ("###ContentSearchInput", Service.Lang.GetText("PleaseSearch"), ref contentSearchInput, 32);

            ImGui.Separator();

            var tableSize = new Vector2(ImGui.GetContentRegionAvail().X, 0);
            if (ImGui.BeginTable("###ContentSelectTable", 5, ImGuiTableFlags.Borders, tableSize))
            {
                ImGui.TableSetupColumn("Checkbox", ImGuiTableColumnFlags.WidthFixed, Styles.CheckboxSize.X);
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

                var selectedCopy = selected;
                var data = PresetData.Contents.OrderByDescending(x => selectedCopy.Contains(x.Key));
                foreach (var contentPair in data)
                {
                    var contentName = contentPair.Value.Name.RawString;
                    var placeName = contentPair.Value.TerritoryType.Value.PlaceName.Value.Name.RawString;
                    if (!string.IsNullOrWhiteSpace(contentSearchInput) &&
                        !contentName.Contains(contentSearchInput, StringComparison.OrdinalIgnoreCase) &&
                        !placeName.Contains(contentSearchInput, StringComparison.OrdinalIgnoreCase)) continue;

                    ImGui.PushID($"{contentName}_{contentPair.Key}");
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    var state = selected.Contains(contentPair.Key);
                    ImGui.Checkbox("", ref state);

                    ImGui.TableNextColumn();
                    ImGui.Image(ImageHelper.GetIcon(contentPair.Value.ContentType.Value.Icon).ImGuiHandle,
                                ImGuiHelpers.ScaledVector2(20f));

                    ImGui.TableNextColumn();
                    ImGui.Text(contentPair.Value.ClassJobLevelRequired.ToString());

                    ImGui.TableNextColumn();
                    if (ImGui.Selectable(contentName, state,
                                         ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.DontClosePopups))
                    {
                        if (!selected.Remove(contentPair.Key)) selected.Add(contentPair.Key);
                        selectState = true;
                    }

                    ImGui.TableNextColumn();
                    ImGui.Text(placeName);

                    ImGui.PopID();
                }

                ImGui.EndTable();
            }

            ImGui.EndPopup();
        }

        return selectState;
    }

    public static bool ActionSelectCombo(ref Action? selectedAction, ref string actionSearchInput)
    {
        var selectState = false;

        if (ImGui.BeginCombo("###ActionSelectCombo",
                             selectedAction == null
                                 ? ""
                                 : $"[{selectedAction.ClassJob.Value?.Name.RawString}] {selectedAction.Name.RawString} ({selectedAction.RowId})",
                             ImGuiComboFlags.HeightLarge))
            ImGui.EndCombo();

        if (ImGui.IsItemClicked())
            ImGui.OpenPopup("###ActionSelectPopup");

        ImGui.SetNextWindowSize(ImGuiHelpers.ScaledVector2(350f, 400f));
        if (ImGui.BeginPopup("###ActionSelectPopup"))
        {
            ImGui.SetNextItemWidth(-1f);
            ImGui.InputTextWithHint
                ("###ActionSearchInput", Service.Lang.GetText("PleaseSearch"), ref actionSearchInput, 32);

            ImGui.Separator();

            var tableSize = new Vector2(ImGui.GetContentRegionAvail().X, 0);
            if (ImGui.BeginTable("###ActionSelectTable", 4, ImGuiTableFlags.Borders, tableSize))
            {
                ImGui.TableSetupColumn("RadioButton", ImGuiTableColumnFlags.WidthFixed, 20f * ImGuiHelpers.GlobalScale);
                ImGui.TableSetupColumn("Job", ImGuiTableColumnFlags.WidthFixed, 70f * ImGuiHelpers.GlobalScale);
                ImGui.TableSetupColumn("Level", ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("1234").X);
                ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthStretch, 50);

                ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
                ImGui.TableNextColumn();
                ImGui.Text("");
                ImGui.TableNextColumn();
                ImGui.Text("职业");
                ImGui.TableNextColumn();
                ImGui.Text("等级");
                ImGui.TableNextColumn();
                ImGui.Text("技能");

                foreach (var actionPair in PresetData.PlayerActions)
                {
                    var actionName = actionPair.Value.Name.RawString;
                    var jobName = actionPair.Value.ClassJob.Value?.Name.RawString ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(actionSearchInput) &&
                        !actionName.Contains(actionSearchInput, StringComparison.OrdinalIgnoreCase) &&
                        !jobName.Contains(actionSearchInput, StringComparison.OrdinalIgnoreCase)) continue;

                    ImGui.PushID($"{actionName}_{actionPair.Key}");
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    ImGui.RadioButton("", selectedAction == actionPair.Value);

                    ImGui.TableNextColumn();
                    ImGui.Text($"{jobName}");

                    ImGui.TableNextColumn();
                    ImGui.Text(actionPair.Value.ClassJobLevel.ToString());

                    ImGui.TableNextColumn();
                    if (ImGuiOm.SelectableImageWithText(ImageHelper.GetIcon(actionPair.Value.Icon).ImGuiHandle,
                                                        ImGuiHelpers.ScaledVector2(20f), actionName, false,
                                                        ImGuiSelectableFlags.DontClosePopups |
                                                        ImGuiSelectableFlags.SpanAllColumns))
                    {
                        selectedAction = actionPair.Value;
                        selectState = true;
                    }

                    ImGui.PopID();
                }

                ImGui.EndTable();
            }

            ImGui.EndPopup();
        }

        return selectState;
    }

    public static bool MountSelectCombo(ref Mount? selectedMount, ref string mountSearchInput)
    {
        var selectState = false;

        if (ImGui.BeginCombo("###MountSelectCombo",
                             selectedMount == null ? "" : $"{selectedMount.Singular.RawString} ({selectedMount.RowId})",
                             ImGuiComboFlags.HeightLarge))
            ImGui.EndCombo();

        if (ImGui.IsItemClicked())
            ImGui.OpenPopup("###MountSelectPopup");

        ImGui.SetNextWindowSize(ImGuiHelpers.ScaledVector2(250f, 400f));
        if (ImGui.BeginPopup("###MountSelectPopup"))
        {
            ImGui.SetNextItemWidth(-1f);
            ImGui.InputTextWithHint
                ("###MountSearchInput", Service.Lang.GetText("PleaseSearch"), ref mountSearchInput, 32);

            ImGui.Separator();

            var tableSize = new Vector2(ImGui.GetContentRegionAvail().X, 0);
            if (ImGui.BeginTable("###MountSelectTable", 3, ImGuiTableFlags.Borders, tableSize))
            {
                ImGui.TableSetupColumn("RadioButton", ImGuiTableColumnFlags.WidthFixed, 20f * ImGuiHelpers.GlobalScale);
                ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.ScaledVector2(20f).X);
                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 200f * ImGuiHelpers.GlobalScale);

                ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
                ImGui.TableNextColumn();
                ImGui.Text("");
                ImGui.TableNextColumn();
                ImGui.Text("");
                ImGui.TableNextColumn();
                ImGui.Text("名称");

                foreach (var mountPair in PresetData.Mounts)
                {
                    var mountName = mountPair.Value.Singular.RawString;
                    if (!string.IsNullOrWhiteSpace(mountSearchInput) &&
                        !mountName.Contains(mountSearchInput, StringComparison.OrdinalIgnoreCase)) continue;

                    ImGui.PushID($"{mountName}_{mountPair.Key}");
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    ImGui.RadioButton("", selectedMount == mountPair.Value);

                    ImGui.TableNextColumn();
                    ImGui.Image(ImageHelper.GetIcon(mountPair.Value.Icon).ImGuiHandle, ImGuiHelpers.ScaledVector2(20f));

                    ImGui.TableNextColumn();
                    if (ImGui.Selectable(mountName, false,
                                         ImGuiSelectableFlags.DontClosePopups | ImGuiSelectableFlags.SpanAllColumns))
                    {
                        selectedMount = mountPair.Value;
                        selectState = true;
                    }

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
