using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using DailyRoutines.Managers;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility;
using ImGuiNET;

namespace DailyRoutines.Helpers;

public class FontHelper
{
    public static IFontAtlas  FontAtlas => fontAtlas.Value;
    public static IFontHandle Icon      => Service.PluginInterface.UiBuilder.IconFontHandle;

    public static IFontHandle UIFont { get; set; } = null!;
    private static float OldFontSize { get; set; }

    private static IFontHandle ConstructFontHandle(GameFontFamilyAndSize fontInfo)
        => FontAtlas.NewGameFontHandle(new GameFontStyle(fontInfo));

    public static void RefreshUIFont()
    {
        if (OldFontSize == Service.Config.InterfaceFontSize) return;
        OldFontSize = Service.Config.InterfaceFontSize;

        string path;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            path = Path.Join("C:\\Windows\\Fonts", "msyh.ttc");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            path = "/System/Library/Fonts/PingFang.ttc";
        }
        else
        {
            throw new PlatformNotSupportedException("不支持的操作系统");
        }

        UIFont = FontAtlas.NewDelegateFontHandle(e =>
        {
            e.OnPreBuild(tk => tk.AddFontFromFile(path, new()
            {
                SizePt = Service.Config.InterfaceFontSize,
                PixelSnapH = true,
                GlyphRanges = BuildRange(null, [ImGui.GetIO().Fonts.GetGlyphRangesChineseFull(), ImGui.GetIO().Fonts.GetGlyphRangesDefault()]),
            }));
        });
    }

    public static unsafe ushort[] BuildRange(IReadOnlyList<ushort>? chars, params nint[] ranges)
    {
        var builder = new ImFontGlyphRangesBuilderPtr(ImGuiNative.ImFontGlyphRangesBuilder_ImFontGlyphRangesBuilder());
        foreach (var range in ranges)
            builder.AddRanges(range);

        if (chars != null)
        {
            for (var i = 0; i < chars.Count; i += 2)
            {
                if (chars[i] == 0)
                    break;

                for (var j = (uint)chars[i]; j <= chars[i + 1]; j++)
                    builder.AddChar((ushort)j);
            }
        }

        for (ushort i = 'A'; i <= 'Z'; i++)
            builder.AddChar(i);
        for (ushort i = 'a'; i <= 'z'; i++)
            builder.AddChar(i);

        builder.AddText("←→↑↓《》■※☀★★☆♥♡ヅツッシ☀☁☂℃℉°♀♂♠♣♦♣♧®©™€$£♯♭♪✓√◎◆◇♦■□〇●△▽▼▲‹›≤≥<«“”─＼～");
        builder.AddText("Œœ");
        builder.AddText("ĂăÂâÎîȘșȚț");

        for (var i = 0x2460; i <= 0x24B5; i++)
            builder.AddChar((char)i);

        builder.AddChar('⓪');
        return builder.BuildRangesToArray();
    }

    public static unsafe ImFontPtr GetFontPtr(float size, float scale = 0)
    {
        var style = new GameFontStyle(GameFontStyle.GetRecommendedFamilyAndSize(GameFontFamily.Axis, size));
        var handle = FontAtlas.NewGameFontHandle(style);

        try
        {
            var font = handle.Lock().ImFont;

            if ((nint)font.NativePtr == nint.Zero)
            {
                return ImGui.GetFont();
            }

            font.Scale = scale == 0 ? size / font.FontSize : scale;
            return font;
        }
        catch
        {
            return ImGui.GetFont();
        }
    }

    public static IFontHandle GetFontHandle(float size, float scale = 0)
    {
        var style = new GameFontStyle(GameFontFamily.Axis, size);
        var handle = FontAtlas.NewGameFontHandle(style);
        
        return handle;
    }

    #region Lazy

    private static Lazy<IFontAtlas> fontAtlas =
        new(() => Service.PluginInterface.UiBuilder.CreateFontAtlas(FontAtlasAutoRebuildMode.OnNewFrame));

    private static Lazy<IFontHandle> axis96 = new(() => ConstructFontHandle(GameFontFamilyAndSize.Axis96));

    private static Lazy<IFontHandle> axis12 = new(() => ConstructFontHandle(GameFontFamilyAndSize.Axis12));

    private static Lazy<IFontHandle> axis14 = new(() => ConstructFontHandle(GameFontFamilyAndSize.Axis14));

    private static Lazy<IFontHandle> axis18 = new(() => ConstructFontHandle(GameFontFamilyAndSize.Axis18));

    #endregion
}
