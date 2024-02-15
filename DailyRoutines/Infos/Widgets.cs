using System.Numerics;
using DailyRoutines.Managers;
using Dalamud.Interface.Colors;
using Dalamud.Interface;
using ECommons.ImGuiMethods;
using ImGuiNET;

namespace DailyRoutines.Infos;

public class Widgets
{
    public static void PreviewImageWithHelpText(string helpText, string imageUrl, Vector2 imageSize, FontAwesomeIcon imageIcon = FontAwesomeIcon.InfoCircle)
    {
        var imageState = ThreadLoadImageHandler.TryGetTextureWrap(imageUrl, out var imageHandler);

        ImGui.TextColored(ImGuiColors.DalamudOrange, helpText);

        ImGui.SameLine();
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.TextDisabled(imageIcon.ToIconString());
        ImGui.PopFont();

        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            if (imageState)
                ImGui.Image(imageHandler.ImGuiHandle, imageSize);
            else
                ImGui.TextDisabled($"{Service.Lang.GetText("ImageLoading")}...");
            ImGui.EndTooltip();
        }
    }
}
