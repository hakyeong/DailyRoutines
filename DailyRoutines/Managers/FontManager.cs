using DailyRoutines.Helpers;
using Dalamud.Interface.ManagedFontAtlas;
using ImGuiNET;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System;
using System.Linq;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.Utility;
using Microsoft.Win32;

namespace DailyRoutines.Managers;

public class FontManager : IDailyManager
{
    public static IFontAtlas  FontAtlas      => fontAtlas.Value;
    public static IFontHandle DefaultFont    => defaultFont.Value;
    public static ushort[]    FontRange      => fontRange.Value;
    public static IFontHandle UIFont         => GetUIFont(1.0f);
    public static IFontHandle UIFont160      => GetUIFont(1.6f);
    public static IFontHandle UIFont140      => GetUIFont(1.4f);
    public static IFontHandle UIFont120      => GetUIFont(1.2f);
    public static IFontHandle UIFont80       => GetUIFont(0.8f);
    public static IFontHandle UIFont60       => GetUIFont(0.6f);
    public static bool        IsFontBuilding => !FontHandleTasks.IsEmpty;

    private delegate int EnumFontFamiliesExProc(ref ENUMLOGFONTEX lpelfe, IntPtr lpntme, uint FontType, IntPtr lParam);

    [DllImport("gdi32.dll", CharSet = CharSet.Auto)]
    private static extern int EnumFontFamiliesEx(
        nint hdc, ref LOGFONT lpLogfont, EnumFontFamiliesExProc lpEnumFontFamExProc, nint lParam, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern nint GetDC(nint hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(nint hWnd, nint hDC);

    private static readonly ConcurrentQueue<float> CreationQueue = [];
    private static readonly ConcurrentDictionary<float, IFontHandle> FontHandles = [];
    private static readonly ConcurrentDictionary<float, Task<IFontHandle>> FontHandleTasks = [];
    public static readonly Dictionary<string, string> InstalledFonts = [];

    #region Lazy

    private static readonly Lazy<IFontAtlas> fontAtlas =
        new(() => Service.PluginInterface.UiBuilder.CreateFontAtlas(FontAtlasAutoRebuildMode.Disable));

    private static readonly Lazy<ushort[]> fontRange = new(() => BuildRange(
                                                               null, ImGui.GetIO().Fonts.GetGlyphRangesChineseFull(),
                                                               ImGui.GetIO().Fonts.GetGlyphRangesDefault()));

    private static readonly Lazy<IFontHandle> defaultFont = new(() => FontAtlas.NewGameFontHandle(new(GameFontFamilyAndSize.Axis18)));

    #endregion

    private void Init()
    {
        RebuildInterfaceFonts();
        RefreshInstalledFonts();
    }

    public static IFontHandle GetUIFont(float scale)
    {
        var actualSize = (float)Math.Round(Service.Config.InterfaceFontSize * scale, 1);
        if (!FontHandles.TryGetValue(actualSize, out var handle))
        {
            NotifyHelper.Debug($"开始构建字体 (字号: {actualSize})");
            if (!FontHandleTasks.TryGetValue(actualSize, out _))
            {
                if (!CreationQueue.Contains(actualSize))
                {
                    CreationQueue.Enqueue(actualSize);
                    _ = ProcessFontCreationQueueAsync();
                }
            }

            return DefaultFont;
        }

        return handle;
    }

    public static IFontHandle GetFont(float size)
    {
        if (!FontHandles.TryGetValue(size, out var handle))
        {
            if (!FontHandleTasks.TryGetValue(size, out _))
            {
                if (!CreationQueue.Contains(size))
                {
                    CreationQueue.Enqueue(size);
                    _ = ProcessFontCreationQueueAsync();
                }
            }

            return DefaultFont;
        }

        return handle;
    }

    public static void RebuildInterfaceFonts()
    {
        GetUIFont(0.9f);
        for (var i = 0.6f; i < 1.8f; i += 0.2f) GetUIFont(i);
    }

    public static void RefreshInstalledFonts() => GetInstalledFonts();

    private static async Task ProcessFontCreationQueueAsync()
    {
        while (CreationQueue.TryDequeue(out var size))
        {
            var task = CreateFontHandle(size);
            FontHandleTasks[size] = task;
            await task;
            await FontAtlas.BuildFontsAsync();
            FontHandleTasks.TryRemove(size, out _);
        }
    }

    private static Task<IFontHandle> CreateFontHandle(float size)
    {
        var path = GetDefaultFontPath();
        var task = Task.Run(() =>
        {
            try
            {
                var handle = FontAtlas.NewDelegateFontHandle(e =>
                {
                    e.OnPreBuild(tk =>
                    {
                        var fileFontPtr = tk.AddFontFromFile(path, new()
                        {
                            SizePt = size,
                            PixelSnapH = true,
                            GlyphRanges = FontRange,
                        });

                        var mixedFontPtr0 = tk.AddGameSymbol(new()
                        {
                            SizePt = size,
                            PixelSnapH = true,
                            MergeFont = fileFontPtr,
                        });

                        tk.AddFontAwesomeIconFont(new()
                        {
                            SizePt = size,
                            PixelSnapH = true,
                            MergeFont = mixedFontPtr0,
                        });
                    });
                });

                FontHandles[size] = handle;
                return handle;
            }
            catch (Exception ex)
            {
                NotifyHelper.Error($"Failed to create font handle for size {size}", ex);
                throw;
            }
        });

        return task;
    }

    private static string GetDefaultFontPath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return Path.Join("C:\\Windows\\Fonts", "msyh.ttc");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "/System/Library/Fonts/PingFang.ttc";

        throw new PlatformNotSupportedException("不支持的操作系统");
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

        builder.AddText("ΑαΒβΓγΔδΕεΖζΗηΘθΙιΚκΛλΜμΝνΞξΟοΠπΡρΣσΤτΥυΦφΧχΨψΩω←→↑↓《》■※☀★★☆♥♡ヅツッシ☀☁☂℃℉°♀♂♠♣♦♣♧®©™€$£♯♭♪✓√◎◆◇♦■□〇●△▽▼▲‹›≤≥<«“”─＼～");
        builder.AddText("Œœ");
        builder.AddText("ĂăÂâÎîȘșȚț");

        for (var i = 0x2460; i <= 0x24B5; i++)
            builder.AddChar((char)i);

        builder.AddChar('⓪');

        return builder.BuildRangesToArray();
    }

    private static int FontEnumProc(ref ENUMLOGFONTEX lpelfe, IntPtr lpntme, uint FontType, IntPtr lParam)
    {
        InstalledFonts.TryAdd(lpelfe.elfFullName, null);
        return 1;
    }

    private static void GetInstalledFonts()
    {
        var logFont = new LOGFONT
        {
            lfCharSet = 1, // DEFAULT_CHARSET
        };

        var hdc = GetDC(IntPtr.Zero);
        _ = EnumFontFamiliesEx(hdc, ref logFont, FontEnumProc, IntPtr.Zero, 0);
        _ = ReleaseDC(IntPtr.Zero, hdc);

        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts");
        if (key != null)
        {
            foreach (var fontName in InstalledFonts.Keys)
            {
                foreach (var valueName in key.GetValueNames())
                {
                    if (valueName.StartsWith(fontName, StringComparison.OrdinalIgnoreCase))
                    {
                        InstalledFonts[fontName] = key.GetValue(valueName).ToString();
                        break;
                    }
                }
            }
        }
    }

    private void Uninit()
    {
        FontHandleTasks.Clear();
        FontHandles.Clear();
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct ENUMLOGFONTEX
    {
        public LOGFONT elfLogFont;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string elfFullName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string elfStyle;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string elfScript;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct LOGFONT
    {
        public int lfHeight;
        public int lfWidth;
        public int lfEscapement;
        public int lfOrientation;
        public int lfWeight;
        public byte lfItalic;
        public byte lfUnderline;
        public byte lfStrikeOut;
        public byte lfCharSet;
        public byte lfOutPrecision;
        public byte lfClipPrecision;
        public byte lfQuality;
        public byte lfPitchAndFamily;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string lfFaceName;
    }
}
