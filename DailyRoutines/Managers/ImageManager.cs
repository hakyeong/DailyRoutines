using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Dalamud.Interface.Internal;
using Dalamud.Plugin.Services;

namespace DailyRoutines.Managers;

public class ImageManager
{
    private class ImageState
    {
        public IDalamudTextureWrap? Image { get; set; }
        public bool IsComplete { get; set; }
    }

    private static readonly HttpClient httpClient = new() { Timeout = TimeSpan.FromSeconds(10) };
    private static volatile bool ThreadRunning;
    private static readonly List<Func<byte[], byte[]>> ConversionsToBitmap = [b => b];

    private static readonly ConcurrentDictionary<string, ImageState> OnlineImages = [];
    private static readonly ConcurrentDictionary<(uint ID, ITextureProvider.IconFlags Flags), ImageState> IconImages = [];

    public static IDalamudTextureWrap? GetIcon(
        uint iconID, ITextureProvider.IconFlags flags = ITextureProvider.IconFlags.None)
    {
        if (IconImages.TryGetValue((iconID, flags), out var image) && image.IsComplete)
            return image.Image;

        var gainedTexture = Service.Texture.GetIcon(iconID, flags);
        if (gainedTexture == null) return null;

        IconImages[(iconID, flags)] = new ImageState { Image = gainedTexture, IsComplete = true };
        return gainedTexture;
    }

    public static bool TryGetIcon(
        uint iconID, out IDalamudTextureWrap? image, ITextureProvider.IconFlags flags = ITextureProvider.IconFlags.None)
    {
        if (IconImages.TryGetValue((iconID, flags), out var gainedImage) && gainedImage.IsComplete)
        {
            image = gainedImage.Image;
            return true;
        }

        image = Service.Texture.GetIcon(iconID, flags);
        if (image == null) return false;

        IconImages[(iconID, flags)] = new ImageState { Image = image, IsComplete = true };
        return true;
    }

    public static IDalamudTextureWrap? GetImage(string url)
    {
        if (!OnlineImages.TryGetValue(url, out var imageState) || !imageState.IsComplete)
        {
            imageState = new();
            OnlineImages[url] = imageState;
            BeginThreadIfNotRunning();
        }

        return imageState.Image;
    }

    public static bool TryGetImage(string url, out IDalamudTextureWrap? image)
    {
        image = null;
        if (OnlineImages.TryGetValue(url, out var imageState) && imageState.IsComplete)
        {
            image = imageState.Image;
            return imageState.Image != null;
        }

        imageState = new();
        OnlineImages[url] = imageState;
        BeginThreadIfNotRunning();

        return imageState.Image != null;
    }

    private static void BeginThreadIfNotRunning()
    {
        if (ThreadRunning) return;
        ThreadRunning = true;
        _ = Task.Run(RunImageLoadingThread);
    }

    private static async Task RunImageLoadingThread()
    {
        var idleTicks = 0;
        while (idleTicks < 100)
            if (OnlineImages.TryGetFirst(x => !x.Value.IsComplete, out var imagePair))
            {
                idleTicks = 0;
                imagePair.Value.IsComplete = true;
                if (imagePair.Key.StartsWith("http:", StringComparison.OrdinalIgnoreCase) ||
                    imagePair.Key.StartsWith("https:", StringComparison.OrdinalIgnoreCase))
                {
                    using var result = await httpClient.GetAsync(imagePair.Key);
                    result.EnsureSuccessStatusCode();
                    var content = await result.Content.ReadAsByteArrayAsync();

                    IDalamudTextureWrap? texture = null;
                    foreach (var conversion in ConversionsToBitmap)
                        try
                        {
                            texture = P.PluginInterface.UiBuilder.LoadImage(conversion(content));
                            break;
                        }
                        catch (Exception ex)
                        {
                            ex.Log();
                        }

                    imagePair.Value.Image = texture;
                }
                else
                {
                    imagePair.Value.Image = File.Exists(imagePair.Key)
                                                ? P.PluginInterface.UiBuilder.LoadImage(imagePair.Key)
                                                : Service.Texture.GetTextureFromGame(imagePair.Key);
                }
            }
            else if (IconImages.TryGetFirst(x => !x.Value.IsComplete, out var iconPair))
            {
                idleTicks = 0;
                iconPair.Value.IsComplete = true;
                iconPair.Value.Image = Service.Texture.GetIcon(iconPair.Key.ID, iconPair.Key.Flags);
            }
            else
            {
                idleTicks++;
                if (OnlineImages.All(x => x.Value.IsComplete) && IconImages.All(x => x.Value.IsComplete))
                    await Task.Delay(100);
            }

        ThreadRunning = false;
    }
}
