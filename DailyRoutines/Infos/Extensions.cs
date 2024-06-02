using System;
using DailyRoutines.Helpers;
using DailyRoutines.Managers;
using DailyRoutines.Modules;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Network.Structures.InfoProxy;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Excel.GeneratedSheets;

namespace DailyRoutines.Infos;

public static class Extensions
{
    /// <summary>
    /// You need to specify the position and category by yourself
    /// </summary>
    /// <param name="macro"></param>
    /// <returns></returns>
    public static QuickChatPanel.SavedMacro ToSavedMacro(this RaptureMacroModule.Macro macro)
    {
        var savedMacro = new QuickChatPanel.SavedMacro
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
            World = LuminaCache.GetRow<World>(((FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)chara.Address)->HomeWorld).Name.RawString
        };
        return info;
    }

    public static unsafe FFXIVClientStructs.FFXIV.Client.Game.Character.Character* ToCharacterStruct(this Character chara)
        => (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)chara.Address;

    public static ExpandPlayerMenuSearch.CharacterSearchInfo ToCharacterSearchInfo(this CharacterData chara)
    {
        var info = new ExpandPlayerMenuSearch.CharacterSearchInfo()
        {
            Name = chara.Name,
            World = chara.HomeWorld.GameData.Name.RawString
        };
        return info;
    }

    public static BitmapFontIcon ToBitmapFontIcon(this ClassJob? job)
    {
        if (job == null || job.RowId == 0) return BitmapFontIcon.NewAdventurer;
        return (BitmapFontIcon)job.RowId + 127;
    }
}
