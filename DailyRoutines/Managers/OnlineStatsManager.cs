using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.Http;
using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using DailyRoutines.Helpers;
using DailyRoutines.Infos;
using DailyRoutines.Modules;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Utility;
using Newtonsoft.Json;
using static DailyRoutines.Windows.Main;

namespace DailyRoutines.Managers;

public class OnlineStatsManager : IDailyManager
{
    public static Dictionary<string, int> ModuleUsageStats { get; private set; } = new(StringComparer.Ordinal);
    public static string?                 MachineCode      { get; private set; }
    public static bool                    IsTimeValid      { get; private set; }
    public static VersionInfo             LatestVersion    { get; private set; } = new();
    public static int                     Downloads_Total  { get; private set; }
    public static string                  Sponsor_Period   { get; private set; } = string.Empty;
    public static List<GameEvent>         GameCalendars    { get; private set; } = [];
    public static List<GameNews>          GameNews         { get; private set; } = [];

    private static readonly string CacheFilePath =
        Path.Join(Service.PluginInterface.GetPluginConfigDirectory(), "OnlineStatsCacheData.json");

    private const string WorkerUrl = "https://spbs.atmoomen.top/";
    private static readonly HttpClient Client = new();

    private static readonly string[] HardwareClasses =
        ["Win32_Processor", "Win32_BaseBoard", "Win32_BIOS", "Win32_LogicalDisk", "Win32_NetworkAdapter",];

    private static readonly string[] Properties =
        ["ProcessorId", "SerialNumber", "SerialNumber", "VolumeSerialNumber", "MACAddress",];

    private static readonly string?[] Conditions = 
        [null, null, null, "DriveType = 3", "PhysicalAdapter = 'True'"];

    private void Init()
    {
        Client.DefaultRequestHeaders.Add("Prefer", "return=minimal");
        Service.ClientState.Login += OnLogin;
        RefreshOnlineInfo();
    }

    private static void RefreshOnlineInfo()
    {
        Task.WhenAll(
            RequestModuleStats(),
            RequestValidityInfo(),
            RequestPluginInfo()
        );
    }

    private static void OnLogin()
    {
        if (Service.Config.AllowAnonymousUpload)
            RefreshOnlineInfo();
    }

    private static async Task RequestModuleStats()
    {
        await DownloadOrLoadModuleStats();
        await UploadEntry(new(GetEncryptedMachineCode()));
    }

    public static async Task DownloadOrLoadModuleStats()
    {
        try
        {
            if (NeedToRefreshData())
                await DownloadModuleUsageStats();

            if (File.Exists(CacheFilePath))
            {
                var jsonData = await File.ReadAllTextAsync(CacheFilePath);
                var statsData = JsonConvert.DeserializeObject<ModuleStats>(jsonData);
                if (statsData?.ModuleUsageStats != null)
                    ModuleUsageStats = new Dictionary<string, int>(statsData.ModuleUsageStats, StringComparer.Ordinal);
            }
        }
        catch (Exception ex)
        {
            NotifyHelper.Error("下载在线或加载本地数据时出现异常", ex);
        }
    }

    private static bool NeedToRefreshData()
    {
        if (!File.Exists(CacheFilePath)) return true;

        var jsonData = File.ReadAllText(CacheFilePath);
        var statsData = JsonConvert.DeserializeObject<ModuleStats>(jsonData);
        return statsData == null || !DateTime.TryParse(statsData.LastUpdated, out var lastUpdateTime) ||
               (DateTime.UtcNow - lastUpdateTime).TotalHours > 2;
    }

    private static async Task DownloadModuleUsageStats()
    {
        var response = await Client.GetAsync(WorkerUrl);

        if (response.IsSuccessStatusCode)
        {
            var jsonContent = await response.Content.ReadAsStringAsync();
            var moduleUsageStats = JsonConvert.DeserializeObject<Dictionary<string, int>>(jsonContent);

            var statsData = new ModuleStats
            {
                LastUpdated = DateTime.UtcNow.ToString("o"),
                ModuleUsageStats = moduleUsageStats,
            };

            await File.WriteAllTextAsync(CacheFilePath, JsonConvert.SerializeObject(statsData));
        }
        else
            NotifyHelper.Debug($"下载模块启用数据失败\n状态码: {response.StatusCode}");
    }

    public static async Task UploadEntry(ModuleStat entry)
    {
        var content = new StringContent(JsonConvert.SerializeObject(entry), Encoding.UTF8, "application/json");
        var response = await Client.PutAsync($"{WorkerUrl}?character=eq.{entry.Character}", content);

        if (!response.IsSuccessStatusCode)
            NotifyHelper.Debug(
                $"上传模块启用数据失败\n状态码: {response.StatusCode} 返回内容: {await response.Content.ReadAsStringAsync()}");
    }

    private static async Task RequestValidityInfo()
    {
        _ = GetEncryptedMachineCode();
        var serverTime = await GetWebDateTimeAsync();
        IsTimeValid = Math.Abs((serverTime - DateTimeOffset.UtcNow).TotalSeconds) <= 60;
    }

    public static async Task<DateTimeOffset> GetWebDateTimeAsync()
    {
        using var handler = new HttpClientHandler();
        handler.UseProxy = false;
        using var client = new HttpClient(handler);
        client.Timeout = TimeSpan.FromSeconds(30);

        try
        {
            var response = await client.GetAsync("http://connectivitycheck.platform.hicloud.com/generate_204");
            response.EnsureSuccessStatusCode();

            if (response.Headers.TryGetValues("Date", out var dateValues))
            {
                var datetime = dateValues.FirstOrDefault();
                if (datetime != null)
                {
                    string[] formats =
                    [
                        "ddd, dd MMM yyyy HH:mm:ss GMT",
                        "ddd, d MMM yyyy HH:mm:ss GMT",
                        "ddd, dd MMM yyyy HH:mm:ss 'GMT'",
                        "ddd, d MMM yyyy HH:mm:ss 'GMT'",
                    ];

                    if (DateTimeOffset.TryParseExact(datetime, formats, CultureInfo.InvariantCulture,
                                                     DateTimeStyles.AssumeUniversal, out var parsedDate))
                        return parsedDate;

                    NotifyHelper.Error($"无法解析日期: {datetime}。尝试的格式: {string.Join(", ", formats)}");
                }
                else
                    NotifyHelper.Error("Date Header 为空");
            }
            else
                NotifyHelper.Error("云端服务器响应中不存在 Date Header");
        }
        catch (Exception ex)
        {
            NotifyHelper.Error("获取在线验证时间时发生未知错误", ex);
        }

        return DateTimeOffset.MinValue;
    }

    public static string GetEncryptedMachineCode()
    {
        if (!string.IsNullOrWhiteSpace(MachineCode)) return MachineCode;

        var machineCodeBuilder = new StringBuilder();
        var hardwareValues = GetHardwareValues();

        foreach (var value in hardwareValues)
            machineCodeBuilder.Append(value);

        using var sha256Hash = SHA256.Create();
        MachineCode = GetHash(sha256Hash, machineCodeBuilder.ToString());
        return MachineCode;
    }

    private static IEnumerable<string> GetHardwareValues()
    {
        for (var i = 0; i < HardwareClasses.Length; i++)
            yield return GetSingleHardwareValue(Properties[i], HardwareClasses[i], Conditions[i]);
    }

    private static string GetSingleHardwareValue(string property, string className, string? condition)
    {
        using var searcher = new ManagementObjectSearcher($"SELECT {property} FROM {className}" +
                                                          (condition != null ? " WHERE " + condition : ""));

        foreach (var obj in searcher.Get())
            return obj[property]?.ToString() ?? string.Empty;

        return string.Empty;
    }

    private static string GetHash(HashAlgorithm hashAlgorithm, string input)
    {
        var data = hashAlgorithm.ComputeHash(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(data).Replace("-", "").ToLowerInvariant();
    }

    private static async Task RequestPluginInfo()
    {
        await Task.WhenAll(
            GetPluginVersionInfo(),
            GetPluginSponsorInfo(),
            GetCNOfficalInformation()
        );
    }

    public static async Task GetPluginVersionInfo()
    {
        _ = ImageHelper.GetImage("https://gh.atmoomen.top/DailyRoutines/main/Assets/Images/Changelog.png");

        var tasks = new[]
        {
            ObtainContentStringFromUrl("https://gh.atmoomen.top/DailyRoutines/main/Assets/downloads.txt"),
            ObtainContentStringFromUrl("https://gh.atmoomen.top/DailyRoutines/main/Assets/downloads_latest.txt"),
            ObtainContentStringFromUrl("https://gh.atmoomen.top/DailyRoutines/main/Assets/changelog.txt"),
            ObtainContentStringFromUrl("https://gh.atmoomen.top/DailyRoutines/main/Assets/version_latest.txt"),
            ObtainContentStringFromUrl("https://gh.atmoomen.top/DailyRoutines/main/Assets/changelog_time.txt"),
        };

        var results = await Task.WhenAll(tasks);

        Downloads_Total = int.TryParse(results[0], out var totalDownloads) ? totalDownloads : 0;
        LatestVersion.DownloadCount = int.TryParse(results[1], out var latestDownloads) ? latestDownloads : 0;
        LatestVersion.Changelog = MarkdownToPlainText(results[2]);
        LatestVersion.Version = Version.TryParse(results[3], out var version) ? version : new Version();
        LatestVersion.PublishTime = results[4];
    }

    public static async Task GetPluginSponsorInfo()
    {
        Sponsor_Period =
            await ObtainContentStringFromUrl("https://gh.atmoomen.top/DailyRoutines/main/Assets/sponsor_period.txt");

        _ = ImageHelper.GetImage("https://gh.atmoomen.top/DailyRoutines/main/Assets/Images/AfdianSponsor.jpg");
    }

    public static async Task GetCNOfficalInformation()
    {
        var calendarTask =
            ObtainContentStringFromUrl(
                "https://apiff14risingstones.web.sdo.com/api/home/active/calendar/getActiveCalendarMonth");

        var newsTask =
            ObtainContentStringFromUrl(
                "https://cqnews.web.sdo.com/api/news/newsList?gameCode=ff&CategoryCode=5309,5310,5311,5312,5313&pageIndex=0&pageSize=5");

        await Task.WhenAll(calendarTask, newsTask);

        var resultCalendar = JsonConvert.DeserializeObject<FileFormat.RSActivityCalendar>(await calendarTask);
        var resultNews = JsonConvert.DeserializeObject<FileFormat.RSGameNews>(await newsTask);

        UpdateGameCalendars(resultCalendar);
        UpdateGameNews(resultNews);
    }

    private static void UpdateGameCalendars(FileFormat.RSActivityCalendar resultCalendar)
    {
        if (resultCalendar.data.Count > 0)
        {
            foreach (var activity in GameCalendars)
                Service.LinkPayloadManager.Unregister(activity.LinkPayloadID);

            GameCalendars.Clear();

            var currentTime = DateTime.Now;
            GameCalendars =
            [
                .. resultCalendar.data.Select(activity =>
                {
                    var beginTime = UnixSecondToDateTime(activity.begin_time);
                    var endTime = UnixSecondToDateTime(activity.end_time);
                    return new GameEvent
                    {
                        ID = activity.id,
                        LinkPayload = Service.LinkPayloadManager.Register(OpenGameEventLinkPayload, out var linkPayloadID),
                        LinkPayloadID = linkPayloadID,
                        Name = activity.name,
                        Url = activity.url,
                        BeginTime = beginTime,
                        EndTime = endTime,
                        Color = DarkenColor(HexToVector4(activity.color), 0.3f),
                        State = currentTime < beginTime ? 1U : currentTime <= endTime ? 0U : 2U,
                        DaysLeft = currentTime < beginTime ? (beginTime - currentTime).Days :
                                   currentTime <= endTime ? (endTime - currentTime).Days : int.MaxValue,
                    };
                }).OrderBy(x => x.DaysLeft),
            ];
        }
    }

    private static void UpdateGameNews(FileFormat.RSGameNews resultNews)
    {
        if (resultNews.Data.Count > 0)
        {
            GameNews = resultNews.Data.Select(activity => new GameNews
            {
                Title = activity.Title,
                Url = activity.Author,
                SortIndex = activity.SortIndex,
                Summary = activity.Summary,
                HomeImagePath = activity.HomeImagePath,
                PublishDate = activity.PublishDate,
            }).ToList();

            foreach (var news in GameNews)
                _ = ImageHelper.GetImage(news.HomeImagePath);

            ImageCarouselInstance.News = GameNews;
        }
    }

    internal static void OpenGameEventLinkPayload(uint commandID, SeString message)
    {
        var link = GameCalendars.FirstOrDefault(x => x.LinkPayloadID == commandID)?.Url;
        if (!string.IsNullOrWhiteSpace(link))
            Util.OpenLink(link);
    }

    public static Task<string> ObtainContentStringFromUrl(string url) => Client.GetStringAsync(url);

    private void Uninit() { Service.ClientState.Login -= OnLogin; }

    public class ModuleStats
    {
        public string?                  LastUpdated      { get; set; }
        public Dictionary<string, int>? ModuleUsageStats { get; set; }
    }

    public class VersionInfo
    {
        public Version Version       { get; set; } = new();
        public string  PublishTime   { get; set; } = string.Empty;
        public string  Changelog     { get; set; } = string.Empty;
        public int     DownloadCount { get; set; }
    }
}

public class ModuleStat
{
    [JsonProperty("character")]
    public string Character = OnlineStatsManager.GetEncryptedMachineCode();

    [JsonProperty("world")]
    public string? World = Service.ClientState.LocalPlayer?.HomeWorld.GameData?.Name.RawString ?? null;

    [JsonProperty("update_time")]
    public string UpdateTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");

    [JsonProperty("version")]
    public string Version = Plugin.Version.ToString();

    [JsonProperty("enabled_modules")]
    public string[] EnabledModules = Service.ModuleManager.Modules
                                            .Where(x => x.Value.Initialized &&
                                                        x.Key.GetCustomAttribute<ModuleDescriptionAttribute>() != null)
                                            .Select(x => Service.Lang.GetText(
                                                        x.Key.GetCustomAttribute<ModuleDescriptionAttribute>()
                                                         ?.TitleKey ?? "DevModuleTitle")).ToArray();

    [JsonProperty("all_modules_amount")]
    public uint AllModulesAmount = (uint)Service.ModuleManager.Modules.Count;

    [JsonProperty("enabled_modules_amount")]
    public uint EnabledModulesAmount = (uint)Service.ModuleManager.Modules.Count(x => x.Value.Initialized);

    public ModuleStat(string character) { Character = character; }

    public ModuleStat() { }
}

public class GameEvent
{
    public uint                ID            { get; set; }
    public DalamudLinkPayload? LinkPayload   { get; set; }
    public uint                LinkPayloadID { get; set; }
    public string              Name          { get; set; } = string.Empty;
    public string              Url           { get; set; } = string.Empty;
    public DateTime            BeginTime     { get; set; } = DateTime.MinValue;
    public DateTime            EndTime       { get; set; } = DateTime.MaxValue;
    public Vector4             Color         { get; set; }

    /// <summary>
    ///     0 - 正在进行; 1 - 未开始; 2 - 已结束
    /// </summary>
    public uint State { get; set; }

    /// <summary>
    ///     如果已结束, 则为 -1
    /// </summary>
    public int DaysLeft { get; set; } = int.MaxValue;
}

public class GameNews
{
    public string Title         { get; set; } = string.Empty;
    public string Url           { get; set; } = string.Empty;
    public string PublishDate   { get; set; } = string.Empty;
    public string Summary       { get; set; } = string.Empty;
    public string HomeImagePath { get; set; } = string.Empty;
    public int    SortIndex     { get; set; }
}
