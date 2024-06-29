using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DailyRoutines.Helpers;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility;
using ImGuiNET;

namespace DailyRoutines.Managers;

public class FontManager : IDailyManager
{
    public static IFontAtlas  FontAtlas      => _fontAtlas.Value;
    public static IFontHandle DefaultFont    => _defaultFont.Value;
    public static ushort[]    FontRange      => _fontRange.Value;
    public static IFontHandle UIFont         => GetUIFont(1.0f);
    public static IFontHandle UIFont160      => GetUIFont(1.6f);
    public static IFontHandle UIFont140      => GetUIFont(1.4f);
    public static IFontHandle UIFont120      => GetUIFont(1.2f);
    public static IFontHandle UIFont80       => GetUIFont(0.8f);
    public static IFontHandle UIFont60       => GetUIFont(0.6f);
    public static bool        IsFontBuilding => !_fontHandleTasks.IsEmpty;

    private static readonly ConcurrentQueue<float> _creationQueue = [];
    private static readonly ConcurrentDictionary<float, IFontHandle> _fontHandles = [];
    private static readonly ConcurrentDictionary<float, Task<IFontHandle>> _fontHandleTasks = [];
    public static readonly ConcurrentDictionary<string, string> InstalledFonts = [];

    #region Lazy

    private static readonly Lazy<IFontAtlas> _fontAtlas =
        new(() => Service.PluginInterface.UiBuilder.CreateFontAtlas(FontAtlasAutoRebuildMode.Disable));

    private static readonly Lazy<ushort[]> _fontRange =
        new(() => BuildRange(null, ImGui.GetIO().Fonts.GetGlyphRangesChineseFull(),
                             ImGui.GetIO().Fonts.GetGlyphRangesDefault()));

    private static readonly Lazy<IFontHandle> _defaultFont =
        new(() => FontAtlas.NewGameFontHandle(new(GameFontFamilyAndSize.Axis18)));

    #endregion

    private void Init()
    {
        RebuildInterfaceFonts(true);
        RefreshInstalledFonts();
    }

    public static IFontHandle GetUIFont(float scale)
    {
        var actualSize = MathF.Round(Service.Config.InterfaceFontSize * scale, 1);
        return GetFont(actualSize);
    }

    public static IFontHandle GetFont(float size)
    {
        if (_fontHandles.TryGetValue(size, out var handle))
            return handle;

        if (!_fontHandleTasks.ContainsKey(size) && !_creationQueue.Contains(size))
        {
            _creationQueue.Enqueue(size);
            _ = ProcessFontCreationQueueAsync();
        }

        return DefaultFont;
    }

    public static void RebuildInterfaceFonts(bool clearOld = false)
    {
        if (clearOld)
        {
            _fontHandleTasks.Clear();
            _fontHandles.Clear();
        }

        var sizes = new[] { 0.6f, 0.8f, 0.9f, 1.0f, 1.2f, 1.4f, 1.6f };
        foreach (var size in sizes)
            GetUIFont(size);
    }

    public static void RefreshInstalledFonts() => GetInstalledFonts();

    private static async Task ProcessFontCreationQueueAsync()
    {
        const int batchSize = 7;
        var batch = new List<float>(batchSize);

        while (_creationQueue.TryDequeue(out var size))
        {
            batch.Add(size);
            var task = CreateFontHandle(size);
            _fontHandleTasks[size] = task;
            await task;

            if (batch.Count >= batchSize || _creationQueue.IsEmpty)
            {
                await FontAtlas.BuildFontsAsync();
                foreach (var completedSize in batch)
                    _fontHandleTasks.TryRemove(completedSize, out _);

                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            await FontAtlas.BuildFontsAsync();
            foreach (var completedSize in batch)
                _fontHandleTasks.TryRemove(completedSize, out _);
        }
    }

    private static Task<IFontHandle> CreateFontHandle(float size)
    {
        return Task.Run(() =>
        {
            try
            {
                var handle = FontAtlas.NewDelegateFontHandle(e =>
                {
                    e.OnPreBuild(tk =>
                    {
                        var fileFontPtr = tk.AddFontFromFile(Service.Config.InterfaceFontFileName, new()
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

                _fontHandles[size] = handle;
                return handle;
            }
            catch (Exception ex)
            {
                NotifyHelper.Error($"Failed to create font handle for size {size}", ex);
                throw;
            }
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

        builder.AddText(
            "ΑαΒβΓγΔδΕεΖζΗηΘθΙιΚκΛλΜμΝνΞξΟοΠπΡρΣσΤτΥυΦφΧχΨψΩω←→↑↓《》■※☀★★☆♥♡ヅツッシ☀☁☂℃℉°♀♂♠♣♦♣♧®©™€$£♯♭♪✓√◎◆◇♦■□〇●△▽▼▲‹›≤≥<«“”─＼～");

        builder.AddText("ŒœĂăÂâÎîȘșȚț");

        for (var i = 0x2460; i <= 0x24B5; i++)
            builder.AddChar((char)i);

        builder.AddChar('⓪');
        var finalArray = builder.BuildRangesToArray();

        builder.Destroy();
        return finalArray;
    }

    public static void GetInstalledFonts()
    {
        var fontDirectories = new[]
        {
            @"C:\Windows\Fonts",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         @"Microsoft\Windows\Fonts"),
        };

        string[] fontExtensions = [".ttf", ".otf", ".ttc", ".otc"];

        foreach (var directory in fontDirectories)
        {
            if (!Directory.Exists(directory)) continue;

            foreach (var file in Directory.EnumerateFiles(directory)
                                          .Where(f => fontExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())))
                try
                {
                    using var pfc = new PrivateFontCollection();
                    pfc.AddFontFile(file);
                    foreach (var family in pfc.Families)
                        InstalledFonts.TryAdd(file, family.Name);
                }
                catch (Exception ex)
                {
                    NotifyHelper.Error($"Error processing file {file}: {ex.Message}");
                }
        }
    }

    private void Uninit()
    {
        _fontHandleTasks.Clear();
        _fontHandles.Clear();
    }
}
