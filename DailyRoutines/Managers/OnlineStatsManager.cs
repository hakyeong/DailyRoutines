using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using DailyRoutines.Infos;
using Newtonsoft.Json;

namespace DailyRoutines.Managers;

public class OnlineStatsManager : IDailyManager
{
    public class ModulesState
    {
        [JsonProperty("character")]
        public string Character = string.Empty;

        [JsonProperty("update_time")]
        public string UpdateTime = DateTime.Now.ToString(CultureInfo.InvariantCulture);

        [JsonProperty("version")]
        public string Version = Plugin.Version.ToString();

        [JsonProperty("enabled_modules")]
        public string[] EnabledModules = Service.ModuleManager.Modules
                                                .Where(x => x.Value.Initialized)
                                                .Select(x => x.Key.Name).ToArray();

        [JsonProperty("all_modules_amount")]
        public uint AllModulesAmount = (uint)Service.ModuleManager.Modules.Count;

        [JsonProperty("enabled_modules_amount")]
        public uint EnabledModulesAmount = (uint)Service.ModuleManager.Modules.Count(x => x.Value.Initialized);

        public ModulesState(string character)
        {
            Character = character;
        }

        public ModulesState() { }
    }

    #region Data
    private const string BaseUrlRest = "https://fygyuifkxhpsruanuqfa.supabase.co/rest/v1/";
    private const string SupabaseAnonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImZ5Z3l1aWZreGhwc3J1YW51cWZhIiwicm9sZSI6ImFub24iLCJpYXQiOjE3MTQxODE0MjAsImV4cCI6MjAyOTc1NzQyMH0.eId2N2z-MXspJ6_z043MoMCujIjyIGtAlwVHEND5VIs";

    private static readonly string[] HardwareClasses =
    [
        "Win32_Processor",
        "Win32_BaseBoard",
        "Win32_BIOS",
        "Win32_LogicalDisk",
        "Win32_NetworkAdapter"
    ];

    private static readonly string[] Properties =
    [
        "ProcessorId",
        "SerialNumber",
        "SerialNumber",
        "VolumeSerialNumber",
        "MACAddress"
    ];

    private static readonly string?[] Conditions =
    [
        null,
        null,
        null,
        "DriveType = 3",
        "PhysicalAdapter = 'True'"
    ];
    #endregion

    private static readonly HttpClient Client = new();

    private void Init()
    {
        Client.DefaultRequestHeaders.Add("apikey", SupabaseAnonKey);
        Client.DefaultRequestHeaders.Add("Authorization", $"Bearer {SupabaseAnonKey}");
        Client.DefaultRequestHeaders.Add("Prefer", "return=minimal");

        Service.ClientState.Login += OnLogin;
    }

    public static async void UploadEntry(ModulesState entry)
    {
        try
        {
            var content = new StringContent(JsonConvert.SerializeObject(entry), Encoding.UTF8, "application/json");
            var response = await Client.PutAsync($"{BaseUrlRest}ModulesState?character=eq.{entry.Character}", content);

            if (response.StatusCode != HttpStatusCode.Created && response.StatusCode != HttpStatusCode.NoContent)
                Service.Log.Debug($"StatusCode: {response.StatusCode} Content: {response.Content.ReadAsStringAsync().Result}");
        }
        catch (Exception e)
        {
            Service.Log.Error(e, "Upload failed");
        }
    }

    public static string GetEncryptedMachineCode()
    {
        var machineCodeBuilder = new StringBuilder();
        var hardwareValues = GetHardwareValues().ToArray();

        foreach (var value in hardwareValues)
        {
            machineCodeBuilder.Append(value);
        }

        var machineCode = machineCodeBuilder.ToString();
        using var sha256Hash = SHA256.Create();
        var encryptedCode = GetHash(sha256Hash, machineCode);

        return encryptedCode;
    }

    private static string[] GetHardwareValues()
    {
        var tasks = new List<Task<string>>(HardwareClasses.Length);

        for (int i = 0; i < HardwareClasses.Length; i++)
        {
            int index = i; // Capture the index for the lambda expression
            tasks.Add(Task.Run(() => GetSingleHardwareValue(Properties[index], HardwareClasses[index], Conditions[index])));
        }

        return Task.WhenAll(tasks).Result;
    }

    private static string GetSingleHardwareValue(string property, string className, string? condition = null)
    {
        using var searcher = new ManagementObjectSearcher($"SELECT {property} FROM {className}" + (condition != null ? " WHERE " + condition : ""));
        var collection = searcher.Get();
        foreach (var o in collection)
        {
            var obj = (ManagementObject)o;
            try
            {
                return obj[property].ToString();
            }
            catch (Exception)
            {
                // ignored
            }
        }
        return string.Empty;
    }

    private static string GetHash(HashAlgorithm hashAlgorithm, string input)
    {
        var data = hashAlgorithm.ComputeHash(Encoding.UTF8.GetBytes(input));

        var sBuilder = new StringBuilder();

        foreach (var t in data)
        {
            sBuilder.Append(t.ToString("x2"));
        }

        return sBuilder.ToString();
    }

    private static void OnLogin()
    {
        if (!Service.Config.AllowAnonymousUpload) return;
        Task.Run(() => UploadEntry(new ModulesState(GetEncryptedMachineCode())));
    }

    private void Uninit()
    {
        Service.ClientState.Login -= OnLogin;
    }
}
