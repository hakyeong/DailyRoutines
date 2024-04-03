using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;

namespace DailyRoutines.Managers;

public class FontManager
{
    public IFontAtlas? FontAtlas { get; private set; }
    public IFontHandle? Axis96 { get; private set; }
    public IFontHandle? Axis12 { get; private set; }
    public IFontHandle? Axis14 { get; private set; }
    public IFontHandle? Axis18 { get; private set; }


    public FontManager()
    {
        FontAtlas ??= P.PluginInterface.UiBuilder.CreateFontAtlas(FontAtlasAutoRebuildMode.OnNewFrame);
        Axis96 ??= ConstructFontHandle(GameFontFamilyAndSize.Axis96);
        Axis12 ??= ConstructFontHandle(GameFontFamilyAndSize.Axis12);
        Axis14 ??= ConstructFontHandle(GameFontFamilyAndSize.Axis14);
        Axis18 ??= ConstructFontHandle(GameFontFamilyAndSize.Axis18);
    }

    private IFontHandle ConstructFontHandle(GameFontFamilyAndSize fontInfo)
    {
        return FontAtlas.NewGameFontHandle(new GameFontStyle(fontInfo));
    }
}
