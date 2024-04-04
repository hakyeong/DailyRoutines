using System;
using System.Collections.Generic;
using System.Numerics;
using DailyRoutines.Managers;
using Dalamud.Interface.Colors;
using Dalamud.Interface;
using Dalamud.Memory;
using ECommons.ImGuiMethods;
using ImGuiNET;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using static DailyRoutines.Modules.QuickChatPanel;

namespace DailyRoutines.Infos;

public static class Widgets
{
    public static void PreviewImageWithHelpText(
        string helpText, string imageUrl, Vector2 imageSize, FontAwesomeIcon imageIcon = FontAwesomeIcon.InfoCircle)
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

    public static void ConflictKeyText()
    {
        ImGui.Text($"{Service.Lang.GetText("ConflictKey")}: {Service.Config.ConflictKey}");
    }

    /// <summary>
    /// You need to specify the position and category by yourself
    /// </summary>
    /// <param name="macro"></param>
    /// <returns></returns>
    public static SavedMacro ToSavedMacro(this RaptureMacroModule.Macro macro)
    {
        var savedMacro = new SavedMacro
        {
            Name = macro.Name.ExtractText(),
            IconID = macro.IconId,
            LastUpdateTime = DateTime.Now
        };

        return savedMacro;
    }
}
