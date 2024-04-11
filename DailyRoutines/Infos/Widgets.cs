using System;
using System.Numerics;
using DailyRoutines.Managers;
using DailyRoutines.Modules;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Network.Structures.InfoProxy;
using Dalamud.Interface.Colors;
using Dalamud.Interface;
using ECommons.ImGuiMethods;
using ImGuiNET;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Excel.GeneratedSheets;
using static DailyRoutines.Modules.QuickChatPanel;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text;

namespace DailyRoutines.Infos;

public static class Widgets
{
    public static SeString RPrefix(string text)
    {
        return new SeStringBuilder()
        .AddUiForeground(SeIconChar.BoxedLetterR.ToIconString(), 34)
        .AddUiForegroundOff().Append(text).Build();
    } 

    public static void PreviewImageWithHelpText(
        string helpText, string imageUrl, Vector2 imageSize = default, FontAwesomeIcon imageIcon = FontAwesomeIcon.InfoCircle)
    {
        var imageState = ThreadLoadImageHandler.TryGetTextureWrap(imageUrl, out var imageHandle);

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

    public static unsafe ExpandPlayerMenuSearch.CharacterSearchInfo ToCharacterSearchInfo(this Character chara)
    {
        var info = new ExpandPlayerMenuSearch.CharacterSearchInfo()
        {
            Name = chara.Name.ExtractText(),
            World = Service.Data.GetExcelSheet<World>()
                           .GetRow(
                               ((FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)chara.Address)->HomeWorld)
                           .Name.RawString
        };
        return info;
    }

    public static ExpandPlayerMenuSearch.CharacterSearchInfo ToCharacterSearchInfo(this CharacterData chara)
    {
        var info = new ExpandPlayerMenuSearch.CharacterSearchInfo()
        {
            Name = chara.Name,
            World = chara.HomeWorld.GameData.Name.RawString
        };
        return info;
    }
}
