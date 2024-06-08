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
using Dalamud.Interface.Internal;
using Dalamud.Interface.Utility;
using ImGuiNET;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using static System.ComponentModel.Design.ObjectSelectorEditor;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using Action = Lumina.Excel.GeneratedSheets.Action;

namespace DailyRoutines.Infos;

public static class Widgets
{
    public static SeString RPrefix => rPrefix.Value;
    public static SeString DRPrefix => drPrefix.Value;
    public static SeString DailyRoutinesPrefix => dailyRoutinesPrefix.Value;

    private static Vector2 CheckboxSize = ImGuiHelpers.ScaledVector2(20f);
    
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
                ImGui.TableSetupColumn("Checkbox", ImGuiTableColumnFlags.WidthFixed, CheckboxSize.X);
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
                    CheckboxSize = ImGui.GetItemRectSize();

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

    public static bool ZoneSelectCombo(ref HashSet<uint> selected, ref string zoneSearchInput)
    {
        var selectState = false;
        if (ImGui.BeginCombo("###ZoneSelectCombo", $"当前已选中 {selected.Count} 个区域", ImGuiComboFlags.HeightLarge))
            ImGui.EndCombo();

        if (ImGui.IsItemClicked())
            ImGui.OpenPopup("###ZoneSelectPopup");

        ImGui.SetNextWindowSize(ImGuiHelpers.ScaledVector2(450f, 400f));
        if (ImGui.BeginPopup("###ZoneSelectPopup"))
        {
            ImGui.SetNextItemWidth(-1f);
            ImGui.InputTextWithHint
                ("###ZoneSearchInput", Service.Lang.GetText("PleaseSearch"), ref zoneSearchInput, 32);

            ImGui.Separator();

            var tableSize = new Vector2(ImGui.GetContentRegionAvail().X, 0);
            if (ImGui.BeginTable("###ZoneSelectTable", 2, ImGuiTableFlags.Borders, tableSize))
            {
                ImGui.TableSetupColumn("Checkbox", ImGuiTableColumnFlags.WidthFixed, CheckboxSize.X);
                ImGui.TableSetupColumn("PlaceName");

                ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
                ImGui.TableNextColumn();
                ImGui.Text("");
                ImGui.TableNextColumn();
                ImGui.Text("区域名");

                var selectedCopy = selected;
                var data = PresetData.Zones.OrderByDescending(x => selectedCopy.Contains(x.Key));
                foreach (var zonePair in data)
                {
                    var placeName = zonePair.Value.PlaceName.Value.Name.RawString;
                    if (!string.IsNullOrWhiteSpace(zoneSearchInput) &&
                        !placeName.Contains(zoneSearchInput, StringComparison.OrdinalIgnoreCase)) continue;

                    ImGui.PushID($"{placeName}_{zonePair.Key}");
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    ImGui.BeginDisabled();
                    var state = selected.Contains(zonePair.Key);
                    ImGui.Checkbox("", ref state);
                    CheckboxSize = ImGui.GetItemRectSize();
                    ImGui.EndDisabled();

                    ImGui.TableNextColumn();
                    if (ImGui.Selectable($"{placeName} ({zonePair.Key})", state,
                                         ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.DontClosePopups))
                    {
                        if (!selected.Remove(zonePair.Key)) selected.Add(zonePair.Key);
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

    public static bool JobSelectCombo(ref HashSet<uint> selected, ref string jobSearchInput)
    {
        var selectState = false;
        if (ImGui.BeginCombo("###JobSelectCombo", $"当前已选中 {selected.Count} 个职业", ImGuiComboFlags.HeightLarge))
            ImGui.EndCombo();

        if (ImGui.IsItemClicked())
            ImGui.OpenPopup("###JobSelectPopup");

        ImGui.SetNextWindowSize(ImGuiHelpers.ScaledVector2(450f, 400f));
        if (ImGui.BeginPopup("###JobSelectPopup"))
        {
            ImGui.SetNextItemWidth(-1f);
            ImGui.InputTextWithHint
                ("###JobSearchInput", Service.Lang.GetText("PleaseSearch"), ref jobSearchInput, 32);

            ImGui.Separator();

            var tableSize = new Vector2(ImGui.GetContentRegionAvail().X, 0);
            if (ImGui.BeginTable("###JobSelectTable", 2, ImGuiTableFlags.Borders, tableSize))
            {
                ImGui.TableSetupColumn("Checkbox", ImGuiTableColumnFlags.WidthFixed, CheckboxSize.X);
                ImGui.TableSetupColumn("Job", ImGuiTableColumnFlags.WidthStretch);

                ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
                ImGui.TableNextColumn();
                ImGui.Text("");
                ImGui.TableNextColumn();
                ImGui.Text("职业");

                var selectedCopy = selected;
                var data = LuminaCache.Get<ClassJob>().OrderByDescending(x => selectedCopy.Contains(x.RowId));
                foreach (var job in data)
                {
                    var jobName = job.RowId == 0 ? "全部职业" : job.Name.RawString;
                    if (string.IsNullOrWhiteSpace(jobName)) continue;
                    var jobIcon = ImageHelper.GetIcon(62100 + (job.RowId == 0 ? 44 : job.RowId));

                    if (!string.IsNullOrWhiteSpace(jobSearchInput) &&
                        !jobName.Contains(jobSearchInput, StringComparison.OrdinalIgnoreCase)) continue;

                    ImGui.PushID($"{jobName}_{job.RowId}");
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    ImGui.BeginDisabled();
                    var state = job.RowId == 0 ? selected.Count == 0 : selected.Contains(job.RowId);
                    ImGui.Checkbox("", ref state);
                    CheckboxSize = ImGui.GetItemRectSize();
                    ImGui.EndDisabled();

                    ImGui.TableNextColumn();
                    if (ImGuiOm.SelectableImageWithText(jobIcon.ImGuiHandle, ImGuiHelpers.ScaledVector2(20f), jobName, state, 
                                                        ImGuiSelectableFlags.DontClosePopups))
                    {
                        if (job.RowId == 0)
                        {
                            selected.Clear();
                            selectState = true;
                        }
                        else
                        {
                            if (!selected.Remove(job.RowId)) selected.Add(job.RowId);
                            selectState = true;
                        }
                    }

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

    public static bool MultiSelectCombo<T>(Dictionary<uint, T> sourceData, ref HashSet<uint> selectedItems, ref string searchInput,
                                       (string Header, ImGuiTableColumnFlags Flags, float Weight)[] headerFuncs, 
                                       Func<T, System.Action>[] displayFuncs) where T : ExcelRow
    {
        var selectState = false;
        if (ImGui.BeginCombo("###SelectCombo", $"当前已选中 {selectedItems.Count} 项", ImGuiComboFlags.HeightLarge))
            ImGui.EndCombo();

        if (ImGui.IsItemClicked())
            ImGui.OpenPopup("###SelectPopup");

        ImGui.SetNextWindowSize(ImGuiHelpers.ScaledVector2(450f, 400f));
        if (ImGui.BeginPopup("###SelectPopup"))
        {
            ImGui.SetNextItemWidth(-1f);
            ImGui.InputTextWithHint("###SearchInput", Service.Lang.GetText("PleaseSearch"), ref searchInput, 32);

            ImGui.Separator();

            var tableSize = new Vector2(ImGui.GetContentRegionAvail().X, 0);
            if (ImGui.BeginTable("###SelectTable", headerFuncs.Length + 1, ImGuiTableFlags.Borders, tableSize))
            {
                ImGui.TableSetupColumn("Checkbox", ImGuiTableColumnFlags.WidthFixed, CheckboxSize.X);
                foreach (var header in headerFuncs)
                {
                    ImGui.TableSetupColumn(header.Header, ImGuiTableColumnFlags.WidthStretch);
                }

                ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
                ImGui.TableNextColumn();
                ImGui.Text("");

                foreach (var header in headerFuncs)
                {
                    ImGui.TableNextColumn();
                    ImGui.Text(header.Header);
                }

                var searchInputCopy = searchInput;
                var selectedItemsCopy = selectedItems;
                var data = sourceData.OrderByDescending(x => selectedItemsCopy.Contains(x.Key));
                foreach (var (rowId, item) in data)
                {
                    if (!string.IsNullOrWhiteSpace(searchInput) && 
                        !displayFuncs.Any(func => func(item).ToString().Contains(searchInputCopy, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    var isSelected = selectedItems.Contains(rowId);
                    if (ImGui.Checkbox("##" + rowId, ref isSelected))
                    {
                        if (isSelected)
                            selectedItems.Add(rowId);
                        else
                            selectedItems.Remove(rowId);

                        selectState = true;
                    }
                    CheckboxSize = ImGui.GetItemRectSize();

                    foreach (var display in displayFuncs)
                    {
                        ImGui.TableNextColumn();
                        display(item).Invoke();
                    }
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

    #region Lazy

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

    #endregion
}
