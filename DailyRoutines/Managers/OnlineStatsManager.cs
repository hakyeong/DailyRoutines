using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using DailyRoutines.Modules;
using Newtonsoft.Json;

namespace DailyRoutines.Managers;

public class OnlineStatsManager : IDailyManager
{
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

    private const string BaseUrlRest = "https://fygyuifkxhpsruanuqfa.supabase.co/rest/v1/";

    private const string SupabaseAnonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImZ5Z3l1aWZreGhwc3J1YW51cWZhIiwicm9sZSI6ImFub24iLCJpYXQiOjE3MTQxODE0MjAsImV4cCI6MjAyOTc1NzQyMH0.eId2N2z-MXspJ6_z043MoMCujIjyIGtAlwVHEND5VIs";

    private static readonly HttpClient Client = new();

    public OnlineStatsManager()
    {
        Client.DefaultRequestHeaders.Add("apikey", SupabaseAnonKey);
        Client.DefaultRequestHeaders.Add("Authorization", $"Bearer {SupabaseAnonKey}");
        Client.DefaultRequestHeaders.Add("Prefer", "return=minimal");
    }

    private void Init()
    {
        Service.ClientState.Login += OnLogin;
        Task.Run(async () => await UploadEntry(new ModulesState(GetEncryptedMachineCode())));
    }

    public static async Task UploadEntry(ModulesState entry)
    {
        var content = new StringContent(JsonConvert.SerializeObject(entry), Encoding.UTF8, "application/json");
        var response = await Client.PutAsync($"{BaseUrlRest}ModulesState?character=eq.{entry.Character}", content);

        if (response.StatusCode != HttpStatusCode.Created && response.StatusCode != HttpStatusCode.NoContent)
            Service.Log.Debug($"StatusCode: {response.StatusCode} Content: {await response.Content.ReadAsStringAsync()}");
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
        Task.Run(async () => await UploadEntry(new ModulesState(GetEncryptedMachineCode())));
    }

    private void Uninit() { Service.ClientState.Login -= OnLogin; }
}
