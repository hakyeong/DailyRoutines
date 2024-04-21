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

    public static void CNWorldSelectCombo(ref World? selectedWorld, ref string worldSearchInput)
    {
        if (ImGui.BeginCombo("###WorldSelectCombo", selectedWorld == null ? "" : selectedWorld.Name.RawString, ImGuiComboFlags.HeightLarge))
        {
            ImGui.InputTextWithHint("###SearchInput", Service.Lang.GetText("PleaseSearch"), ref worldSearchInput, 32);

            ImGui.Separator();
            foreach (var world in Worlds)
            {
                var worldName = world.Value.Name.RawString;
                var dcName = world.Value.DataCenter.Value.Name.RawString;
                if (!string.IsNullOrWhiteSpace(WorldSearchInput) && !worldName.Contains(WorldSearchInput) && !dcName.Contains(WorldSearchInput)) continue;

                if (ImGui.Selectable($"[{dcName}] {worldName}", SelectedWorld != null && SelectedWorld.RowId == world.Key))
                    SelectedWorld = world.Value;

                ImGui.Separator();
            }

            ImGui.EndCombo();
        }
    }

    public static void ConflictKeyText()
    {
        ImGui.Text($"{Service.Lang.GetText("ConflictKey")}: {Service.Config.ConflictKey}");
    }
}
