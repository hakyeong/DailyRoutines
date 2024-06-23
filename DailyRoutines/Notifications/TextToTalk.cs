using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using DailyRoutines.Helpers;
using DailyRoutines.Managers;

namespace DailyRoutines.Notifications;

// Thanks FFCafe
public class TextToTalk : DailyNotificationBase
{
    public override NotifyType NotifyType => NotifyType.TextToTalk;

    private static readonly HashSet<char> IllegalChars = ['\\', '/', '*', '>', '<', '?', '|', '\"', ':'];

    public override void Init() { }

    public static void Speak(string content, bool cancelPrevious) { Task.Run(() => Speak(content)); }

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
            NotifyHelper.Debug("Failed to download or verify the mp3 file.");
    }

    public static async Task Speak(string text)
    {
        var fileName = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
        var downloadedFilePath = Path.Combine(CacheDirectory, $"{fileName}.wav");
        if (File.Exists(downloadedFilePath))
        {
            var player = new PlayAudioHelper();
            player.Play(downloadedFilePath);
            return;
        }

        text = SecurityElement.Escape(text);
        var ssml = AzureWSSynthesiser.CreateSSML(text, 100, 100, 200, "zh-CN-YunyangNeural", "general", 100, "Default");
        var url = Encoding.UTF8.GetString(Convert.FromBase64String("aHR0cHM6Ly90dHNwcm8ueGl2Y2RuLmNvbS90dHMvdjE="));
        var httpClient = new HttpClient();

        httpClient.DefaultRequestHeaders.Add(Encoding.UTF8.GetString(Convert.FromBase64String("RkZDYWZlLUFjY2Vzcy1Ub2tlbg==")), Encoding.UTF8.GetString(Convert.FromBase64String("OWN1YTVzbjhjYjM4NWJ6YTI3Z2pnY2E1c2p4OHJ3Zm4=")));
        httpClient.DefaultRequestHeaders.Add("Output-Format", "audio-24khz-48kbitrate-mono-mp3");
        httpClient.DefaultRequestHeaders.Add("Voice-Variant", "zh-CN-YunyangNeural".ToLower());
        httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");

        var content = new ByteArrayContent(Encoding.UTF8.GetBytes(ssml));
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/ssml+xml");

        var response = await httpClient.PostAsync(url, content);

        if (response.IsSuccessStatusCode)
        {
            await using var fileStream = new FileStream(downloadedFilePath, FileMode.Create, FileAccess.Write);
            await response.Content.CopyToAsync(fileStream);
            var player = new PlayAudioHelper();
            player.Play(downloadedFilePath);
        }
    }

    public static async Task<bool> DownloadFileAsync(string url, string filePath)
    {
        Directory.CreateDirectory(CacheDirectory);

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");

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

    private static class AzureWSSynthesiser
    {
        public static string CreateSSML(
            string text,
            int speed,
            int pitch,
            int volume,
            string voice,
            string style = null,
            int styleDegree = 100,
            string role = null
        )
        {
            var adjustedStyleDegree = Math.Max(1, styleDegree / 100.0f);
            var styleAttributes = string.IsNullOrEmpty(style) ? "" : $" style=\"{style}\" styledegree=\"{adjustedStyleDegree}\"";
            var roleAttribute = string.IsNullOrEmpty(role) ? "" : $" role=\"{role}\"";

            var expressAs = (!string.IsNullOrEmpty(style) || !string.IsNullOrEmpty(role))
                                ? $"<mstts:express-as{styleAttributes}{roleAttribute}>{text}</mstts:express-as>"
                                : text;

            return $"<speak xmlns=\"http://www.w3.org/2001/10/synthesis\" xmlns:mstts=\"http://www.w3.org/2001/mstts\" version=\"1.0\" xml:lang=\"en-US\">" +
                   $"<voice name=\"{voice}\">" +
                   $"<prosody rate=\"{speed - 100}%\" pitch=\"{(pitch - 100) / 2}%\" volume=\"{Math.Clamp(volume, 1, 100)}\">" +
                   expressAs +
                   "</prosody></voice></speak>";
        }
    }
}
