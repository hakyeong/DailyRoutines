using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DailyRoutines.Managers;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility;
using ImGuiNET;

namespace DailyRoutines.Helpers;

public class FontHelper
{
    public static IFontAtlas  FontAtlas   => fontAtlas.Value;
    public static IFontHandle DefaultFont => defaultFont.Value;
    public static ushort[]    FontRange   => fontRange.Value;
    public static IFontHandle Icon        => Service.PluginInterface.UiBuilder.IconFontHandle;

    private static readonly ConcurrentQueue<float> creationQueue = [];
    private static readonly ConcurrentDictionary<float, IFontHandle> fontHandles = [];
    private static readonly ConcurrentDictionary<float, Task<IFontHandle>> fontHandleTasks = [];

    public static IFontHandle UIFont    => GetUIFont(1.0f);
    public static IFontHandle UIFont160 => GetUIFont(1.6f);
    public static IFontHandle UIFont140 => GetUIFont(1.4f);
    public static IFontHandle UIFont120 => GetUIFont(1.2f);
    public static IFontHandle UIFont80  => GetUIFont(0.8f);
    public static IFontHandle UIFont60  => GetUIFont(0.6f);

    public static IFontHandle GetUIFont(float scale)
    {
        var actualSize = Service.Config.InterfaceFontSize * scale;
        if (!fontHandles.TryGetValue(actualSize, out var handle))
        {
            if (!fontHandleTasks.TryGetValue(actualSize, out _))
            {
                if (!creationQueue.Contains(actualSize))
                {
                    creationQueue.Enqueue(actualSize);
                    _ = ProcessFontCreationQueueAsync();
                }
            }

            return DefaultFont;
        }

        return handle;
    }

    public static IFontHandle GetFont(float size)
    {
        if (!fontHandles.TryGetValue(size, out var handle))
        {
            if (!fontHandleTasks.TryGetValue(size, out _))
            {
                if (!creationQueue.Contains(size))
                {
                    creationQueue.Enqueue(size);
                    _ = ProcessFontCreationQueueAsync();
                }
            }

            return DefaultFont;
        }

        return handle;
    }


    private static async Task ProcessFontCreationQueueAsync()
    {
        while (creationQueue.TryDequeue(out var size))
        {
            var task = CreateFontHandle(size);
            fontHandleTasks[size] = task;
            await task;
            fontHandleTasks.TryRemove(size, out _);
        }
    }

    private static Task<IFontHandle> CreateFontHandle(float size)
    {
        var path = GetFontPath();
        var task = Task.Run(() =>
        {
            var handle = FontAtlas.NewDelegateFontHandle(e =>
            {
                e.OnPreBuild(tk => tk.AddFontFromFile(path, new()
                {
                    SizePt = size,
                    PixelSnapH = true,
                    GlyphRanges = FontRange,
                }));
            });

            fontHandles[size] = handle;
            return handle;
        });

        return task;
    }

    private static string GetFontPath()
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

        builder.AddText("←→↑↓《》■※☀★★☆♥♡ヅツッシ☀☁☂℃℉°♀♂♠♣♦♣♧®©™€$£♯♭♪✓√◎◆◇♦■□〇●△▽▼▲‹›≤≥<«“”─＼～");
        builder.AddText("Œœ");
        builder.AddText("ĂăÂâÎîȘșȚț");

        for (var i = 0x2460; i <= 0x24B5; i++)
            builder.AddChar((char)i);

        builder.AddChar('⓪');
        return builder.BuildRangesToArray();
    }

    #region Lazy

    private static readonly Lazy<IFontAtlas> fontAtlas =
        new(() => Service.PluginInterface.UiBuilder.CreateFontAtlas(FontAtlasAutoRebuildMode.Async));

    private static readonly Lazy<ushort[]> fontRange = new(() => BuildRange(
                                                               null, ImGui.GetIO().Fonts.GetGlyphRangesChineseFull(),
                                                               ImGui.GetIO().Fonts.GetGlyphRangesDefault()));

    private static readonly Lazy<IFontHandle> defaultFont = new(() => Service.PluginInterface.UiBuilder.DefaultFontHandle);

    #endregion
}
