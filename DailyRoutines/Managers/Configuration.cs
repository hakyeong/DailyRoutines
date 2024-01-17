using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DailyRoutines.Infos;
using Dalamud.Configuration;
using Dalamud.Plugin;

namespace DailyRoutines.Managers;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;
    public string SelectedLanguage { get; set; } = string.Empty;
    public Dictionary<string, bool> ModuleEnabled { get; set; } = new();
    public Dictionary<string, object> ModuleConfigurations { get; set; } = new();

    [NonSerialized]
    private DalamudPluginInterface? pluginInterface;
    private static readonly ConcurrentDictionary<Type, string> _typeNameCache = new ConcurrentDictionary<Type, string>();

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
        if (string.IsNullOrEmpty(key))
        {
            return default;
        }

        var moduleName = _typeNameCache.GetOrAdd(module, m => m.Name);
        var actualKey = $"{moduleName}-{key}";

        if (!ModuleConfigurations.TryGetValue(actualKey, out var value))
        {
            return default;
        }

        if (value is T value1) return value1;

        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return default;
        }
    }

    public bool AddConfig<T>(Type module, string key, T value)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException(@"Key cannot be null or empty", nameof(key));
        }

        var actualKey = $"{module.Name}-{key}";

        return (value != null && !ModuleConfigurations.TryAdd(actualKey, value));
    }

    public bool RemoveConfig(Type module, string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException(@"Key cannot be null or empty", nameof(key));
        }

        var actualKey = $"{module.Name}-{key}";

        return ModuleConfigurations.Remove(actualKey, out _);
    }

    public bool UpdateConfig<T>(Type module, string key, T newValue)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException(@"Key cannot be null or empty", nameof(key));
        }

        var actualKey = $"{module.Name}-{key}";

        if (ModuleConfigurations.TryGetValue(actualKey, out var existingValue))
        {
            if (existingValue is T)
            {
                ModuleConfigurations[actualKey] = newValue;
                return true;
            }
        }
        return false;
    }

    public string GetConfigPath() => pluginInterface.ConfigFile.FullName;

    public void Uninitialize()
    {

    }
}
