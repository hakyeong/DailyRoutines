using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using DailyRoutines.Helpers;
using DailyRoutines.Managers;
using WMPLib;

namespace DailyRoutines.Notifications;

public class TextToTalk : DailyNotificationBase
{
    public override NotifyType NotifyType => NotifyType.TextToTalk;

    private static readonly HashSet<char> IllegalChars = ['\\', '/', '*', '>', '<', '?', '|', '\"', ':'];

    public override void Init() { }

    public static void Speak(string content, bool cancelPrevious)
    {
        Task.Run(() => SpeakAsync(content));
    }

    public static async Task SpeakAsync(string text)
    {
        foreach (var illegalChar in IllegalChars)
            text = text.Replace(illegalChar, ' ');

        var url0 = $"https://tts.xivcdn.com/api/say?voice=yaoyao&text={text}&rate=0";
        var downloadedFilePath = Path.Combine(CacheDirectory, "tts.wav");
        var isDownloaded = await DownloadFileAsync(url0, downloadedFilePath);
        if (isDownloaded)
        {
            try
            {
                var player = new PlayAudioHelper();
                player.Play(downloadedFilePath);
            }
            catch (Exception ex)
            {
                NotifyHelper.Debug($"Error playing media file: {ex.Message}");
            }
        }
        else
        {
            NotifyHelper.Debug("Failed to download or verify the mp3 file.");
        }
    }

    public static async Task<bool> DownloadFileAsync(string url, string filePath)
    {
        Directory.CreateDirectory(CacheDirectory);

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");

        try
        {
            using var response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            await using var contentStream = await response.Content.ReadAsStreamAsync();
            if (contentStream.Length > 0)
            {
                await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
                await contentStream.CopyToAsync(fileStream);
                return fileStream.Length > 0;
            }
        }
        catch (Exception ex)
        {
            Service.Log.Error(ex.Message);
            return false;
        }

        return false;
    }

    public override void Uninit() { }
}
