using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DailyRoutines.Infos;
using Dalamud.Configuration;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Plugin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DailyRoutines.Managers;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;
    public string SelectedLanguage { get; set; } = string.Empty;
    public VirtualKey ConflictKey { get; set; } = VirtualKey.SHIFT;
    public Dictionary<string, bool> ModuleEnabled { get; set; } = new();

    [NonSerialized]
    private DalamudPluginInterface? pluginInterface;

    public void Initialize(DalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;

        var assembly = Assembly.GetExecutingAssembly();
        var moduleTypes = assembly.GetTypes()
                                  .Where(t => typeof(IDailyModule).IsAssignableFrom(t) && t.IsClass);

        foreach (var module in moduleTypes)
        {
            ModuleEnabled.TryAdd(module.Name, false);
        }
        Save();
    }

    public void Save()
    {
        pluginInterface!.SavePluginConfig(this);
    }

    public T GetConfig<T>(Type module, string key)
    {
        try
        {
            var configDirectory = P.PluginInterface.GetPluginConfigDirectory();
            var configFile = Path.Combine(configDirectory, module.Name + ".json");

            if (!File.Exists(configFile))
            {
                Service.Log.Error($"Config file not found: {configFile}");
                return default;
            }

            var existingJson = File.ReadAllText(configFile);
            var existingConfig = JsonConvert.DeserializeObject<Dictionary<string, JToken>>(existingJson);

            if (existingConfig == null)
            {
                Service.Log.Error("Failed to deserialize JSON to Dictionary.");
                return default;
            }

            if (existingConfig.TryGetValue(key, out var value))
            {
                try
                {
                    var configValue = value.ToObject<T>();
                    if (configValue == null)
                    {
                        Service.Log.Error($"Failed to convert JToken to type {typeof(T).Name}");
                    }
                    return configValue;
                }
                catch (Exception ex)
                {
                    Service.Log.Error(ex, $"Exception while converting JToken to type {typeof(T).Name}");
                    return default;
                }
            }
            else
            {
                Service.Log.Error($"Key '{key}' not found in the config file.");
                return default;
            }
        }
        catch (Exception ex)
        {
            Service.Log.Error(ex, $"Failed to get config for {module.Name}");
            return default;
        }
    }

    public bool AddConfig(Type module, string key, object config)
    {
        try
        {
            var configDirectory = P.PluginInterface.GetPluginConfigDirectory();
            var configFile = Path.Combine(configDirectory, module.Name + ".json");

            Dictionary<string, object>? existingConfig;

            if (File.Exists(configFile))
            {
                var existingJson = File.ReadAllText(configFile);
                existingConfig = JsonConvert.DeserializeObject<Dictionary<string, object>>(existingJson);
            }
            else
                existingConfig = [];

            existingConfig[key] = config;

            var jsonString = JsonConvert.SerializeObject(existingConfig, Formatting.Indented);

            File.WriteAllText(configFile, jsonString);

            return true;
        }
        catch (Exception ex)
        {
            Service.Log.Error(ex, $"Failed to write config for {module.Name}");
            return false;
        }
    }

    public bool UpdateConfig(Type module, string key, object newConfig)
    {
        try
        {
            var configDirectory = P.PluginInterface.GetPluginConfigDirectory();
            var configFile = Path.Combine(configDirectory, module.Name + ".json");

            if (!File.Exists(configFile))
            {
                Service.Log.Error($"Config file for {module.Name} does not exist.");
                return false;
            }

            var existingJson = File.ReadAllText(configFile);
            var existingConfig = JsonConvert.DeserializeObject<Dictionary<string, object>>(existingJson);

            if (!existingConfig.ContainsKey(key))
            {
                Service.Log.Error($"Key '{key}' does not exist in the config for {module.Name}.");
                return false;
            }

            existingConfig[key] = newConfig;

            var jsonString = JsonConvert.SerializeObject(existingConfig, Formatting.Indented);
            File.WriteAllText(configFile, jsonString);

            return true;
        }
        catch (Exception ex)
        {
            Service.Log.Error(ex, $"Failed to update config for {module.Name}");
            return false;
        }
    }

    public bool RemoveConfig(Type module, string key)
    {
        try
        {
            var configDirectory = P.PluginInterface.GetPluginConfigDirectory();
            var configFile = Path.Combine(configDirectory, module.Name + ".json");

            Dictionary<string, object>? existingConfig;

            if (File.Exists(configFile))
            {
                var existingJson = File.ReadAllText(configFile);
                existingConfig = JsonConvert.DeserializeObject<Dictionary<string, object>>(existingJson);
            }
            else
                return false;

            if (!existingConfig.Remove(key)) return false;

            var jsonString = JsonConvert.SerializeObject(existingConfig, Formatting.Indented);

            File.WriteAllText(configFile, jsonString);

            return true;
        }
        catch (Exception ex)
        {
            Service.Log.Error(ex, $"Failed to remove config for {module.Name}");
            return false;
        }
    }

    public bool ConfigExists(Type module, string key)
    {
        try
        {
            var configDirectory = P.PluginInterface.GetPluginConfigDirectory();
            var configFile = Path.Combine(configDirectory, module.Name + ".json");

            if (!File.Exists(configFile))
            {
                return false;
            }

            var existingJson = File.ReadAllText(configFile);
            var existingConfig = JsonConvert.DeserializeObject<Dictionary<string, object>>(existingJson);

            return existingConfig.ContainsKey(key);
        }
        catch (Exception ex)
        {
            Service.Log.Error(ex, $"Failed to check key existence for {module.Name}");
            return false;
        }
    }

    public void Uninitialize()
    {

    }
}
