using DailyRoutines.Helpers;
using Dalamud.Interface.ManagedFontAtlas;
using ImGuiNET;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System;
using System.Linq;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.Utility;
using System.Drawing.Text;
using System.Runtime.InteropServices;

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

    private const int FR_PRIVATE = 0x10;
    private const int FR_NOT_ENUM = 0x20;

    [DllImport("gdi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int AddFontResourceEx(string lpszFilename, uint fl, IntPtr pdv);

    private static readonly ConcurrentQueue<float> CreationQueue = [];
    private static readonly ConcurrentDictionary<float, IFontHandle> FontHandles = [];
    private static readonly ConcurrentDictionary<float, Task<IFontHandle>> FontHandleTasks = [];
    public  static readonly ConcurrentDictionary<string, string> InstalledFonts = [];

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
        RebuildInterfaceFonts(true);
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

    public static void RebuildInterfaceFonts(bool clearOld = false)
    {
        if (clearOld)
        {
            FontHandleTasks.Clear();
            FontHandles.Clear();
        }

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
        var path = Service.Config.InterfaceFontFileName;
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

    public static void GetInstalledFonts()
    {
        var fontDirectories = new List<string>
        {
            @"C:\Windows\Fonts",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Windows\Fonts"),
        };

        string[] fontExtensions = ["*.ttf", "*.otf", "*.ttc", "*.otc"];

        foreach (var directory in fontDirectories)
        {
            foreach (var extension in fontExtensions)
            {
                foreach (var file in Directory.GetFiles(directory, extension))
                {
                    try
                    {
                        using var pfc = new PrivateFontCollection();
                        pfc.AddFontFile(file);
                        foreach (var family in pfc.Families)
                        {
                            InstalledFonts.TryAdd(file, family.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        NotifyHelper.Error($"Error processing file {file}: {ex.Message}");
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
}
