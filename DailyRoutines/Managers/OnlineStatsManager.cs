using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using DailyRoutines.Helpers;
using DailyRoutines.Modules;
using Newtonsoft.Json;

namespace DailyRoutines.Managers;

public class OnlineStatsManager : IDailyManager
{
    public static Dictionary<string, int> ModuleUsageStats { get; private set; } = [];

    private static string CacheFilePath =>
        Path.Join(Service.PluginInterface.GetPluginConfigDirectory(), "OnlineStatsCacheData.json");

    private const string WorkerUrl = "https://spbs.atmoomen.top/";

    private static readonly HttpClient Client = new();

    public OnlineStatsManager() { Client.DefaultRequestHeaders.Add("Prefer", "return=minimal"); }

    private void Init()
    {
        Service.ClientState.Login += OnLogin;
        TryUploadAndDownload();
    }

    private static void TryUploadAndDownload()
    {
        Task.Run(async () =>
        {
            await UploadEntry(new ModulesState(GetEncryptedMachineCode()));
            await DownloadOrLoadModuleStats();
        });
    }

    public static async Task DownloadOrLoadModuleStats()
    {
        try
        {
            if (NeedToRefreshData())
                await DownloadModuleUsageStats();

            if (File.Exists(CacheFilePath))
            {
                var jsonData = File.ReadAllText(CacheFilePath);
                var statsData = JsonConvert.DeserializeObject<StatsData>(jsonData);
                if (statsData is { ModuleUsageStats: not null })
                    ModuleUsageStats = statsData.ModuleUsageStats;
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
        var statsData = JsonConvert.DeserializeObject<StatsData>(jsonData);
        if (statsData != null && DateTime.TryParse(statsData.LastUpdated, out var lastUpdateTime))
            return (DateTime.UtcNow - lastUpdateTime).TotalHours > 2;

        return true;
    }

    private static async Task DownloadModuleUsageStats()
    {
        var response = await Client.GetAsync(WorkerUrl);

        if (response.IsSuccessStatusCode)
        {
            var jsonContent = await response.Content.ReadAsStringAsync();
            var moduleUsageStats = JsonConvert.DeserializeObject<Dictionary<string, int>>(jsonContent);

            var statsData = new StatsData
            {
                LastUpdated = DateTime.UtcNow.ToString("o"),
                ModuleUsageStats = moduleUsageStats,
            };

            File.WriteAllText(CacheFilePath, JsonConvert.SerializeObject(statsData));
        }
        else
            NotifyHelper.Debug($"下载模块启用数据失败\n" +
                               $"状态码: {response.StatusCode}");
    }

    public static async Task UploadEntry(ModulesState entry)
    {
        var content = new StringContent(JsonConvert.SerializeObject(entry), Encoding.UTF8, "application/json");
        var response = await Client.PutAsync($"{WorkerUrl}?character=eq.{entry.Character}", content);

        if (response.StatusCode != HttpStatusCode.OK)
            NotifyHelper.Debug(
                $"上传模块启用数据失败\n" +
                $"状态码: {response.StatusCode} 返回内容: {await response.Content.ReadAsStringAsync()}");
    }

    public static string GetEncryptedMachineCode()
    {
        var machineCodeBuilder = new StringBuilder();
        var hardwareValues = GetHardwareValues();

        foreach (var value in hardwareValues)
            machineCodeBuilder.Append(value);

        using var sha256Hash = SHA256.Create();
        return GetHash(sha256Hash, machineCodeBuilder.ToString());
    }

    private static IEnumerable<string> GetHardwareValues()
    {
        string[] HardwareClasses =
        [
            "Win32_Processor",
            "Win32_BaseBoard",
            "Win32_BIOS",
            "Win32_LogicalDisk",
            "Win32_NetworkAdapter",
        ];

        string[] Properties =
        [
            "ProcessorId",
            "SerialNumber",
            "SerialNumber",
            "VolumeSerialNumber",
            "MACAddress",
        ];

        string?[] Conditions =
        [
            null,
            null,
            null,
            "DriveType = 3",
            "PhysicalAdapter = 'True'",
        ];

        for (var i = 0; i < HardwareClasses.Length; i++)
            yield return GetSingleHardwareValue(Properties[i], HardwareClasses[i], Conditions[i]);
    }

    private static string GetSingleHardwareValue(string property, string className, string? condition)
    {
        using var searcher =
            new ManagementObjectSearcher($"SELECT {property} FROM {className}" +
                                         (condition != null ? " WHERE " + condition : ""));

        foreach (var obj in searcher.Get())
            return obj[property]?.ToString() ?? string.Empty;

        return string.Empty;
    }

    private static string GetHash(HashAlgorithm hashAlgorithm, string input)
    {
        var data = hashAlgorithm.ComputeHash(Encoding.UTF8.GetBytes(input));
        var sBuilder = new StringBuilder();
        foreach (var b in data)
            sBuilder.Append(b.ToString("x2"));

        return sBuilder.ToString();
    }

    private static void OnLogin()
    {
        if (!Service.Config.AllowAnonymousUpload) return;
        TryUploadAndDownload();
    }

    private void Uninit() { Service.ClientState.Login -= OnLogin; }

    public class ModulesState
    {
        [JsonProperty("character")]
        public string Character = string.Empty;

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

        public ModulesState(string character) { Character = character; }

        public ModulesState() { }
    }

    public class StatsData
    {
        public string?                  LastUpdated      { get; set; }
        public Dictionary<string, int>? ModuleUsageStats { get; set; }
    }
}
