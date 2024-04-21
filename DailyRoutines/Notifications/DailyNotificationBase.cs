using DailyRoutines.Managers;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace DailyRoutines.Notifications;

public enum NotifyType
{
    Text,
    TextToTalk,
    Push
}

public abstract class DailyNotificationBase
{
    protected string CacheDirectory { get; } = Path.Combine(Service.PluginInterface.GetPluginConfigDirectory(), "Cache");
    private static readonly HttpClient httpClient = new() { Timeout = TimeSpan.FromSeconds(10) };

    public bool Initialized { get; internal set; }
    public virtual NotifyType NotifyType { get; internal set; } = NotifyType.Text;

    public virtual void Init() { }

    public virtual void Uninit() { }

    public async Task DownloadFileAsync(string url)
    {
        Directory.CreateDirectory(CacheDirectory);

        var fileName = Path.GetFileName(new Uri(url).LocalPath);
        var filePath = Path.Combine(CacheDirectory, fileName);

        try
        {
            using var response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            await using var fs = new FileStream(filePath, FileMode.CreateNew);
            await response.Content.CopyToAsync(fs);
        }
        catch (Exception ex)
        {
            Service.Log.Error(ex.Message);
        }
    }
}
