using System.Collections.Generic;
using Dalamud.Interface.Internal;
using Dalamud.Plugin.Services;

namespace DailyRoutines.Managers;

public class IconManager
{
    private static readonly Dictionary<uint, IDalamudTextureWrap> SavedTexture = [];

    public static IDalamudTextureWrap? GetIcon(uint iconID, ITextureProvider.IconFlags flags = ITextureProvider.IconFlags.None)
    {
        if (SavedTexture.TryGetValue(iconID, out var texture))
            return texture;

        var gainedTexture = Service.Texture.GetIcon(iconID, flags);
        if (gainedTexture != null)
            SavedTexture[iconID] = gainedTexture;

        return gainedTexture;
    }
}
