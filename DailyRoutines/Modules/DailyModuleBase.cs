using DailyRoutines.Managers;
using DailyRoutines.Windows;
using Dalamud.Hooking;
using ECommons.Automation;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System;
using System.Linq;
using System.Reflection;
using Dalamud.Interface.Internal.Notifications;

namespace DailyRoutines.Modules;

public abstract class DailyModuleBase
{
    public bool Initialized { get; internal set; }
    public virtual string? Author { get; set; }
    protected TaskManager? TaskManager { get; set; }
    protected Overlay? Overlay { get; set; }

    public virtual void Init() { }

    public virtual void ConfigUI() { }

    public virtual void OverlayUI() { }

    protected static T GetConfig<T>(DailyModuleBase moduleBase, string key)
    {
        try
        {
            var configDirectory = P.PluginInterface.GetPluginConfigDirectory();
            var configFile = Path.Combine(configDirectory, moduleBase.GetType().Name + ".json");

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
                    if (configValue == null) Service.Log.Error($"Failed to convert JToken to type {typeof(T).Name}");
                    return configValue;
                }
                catch (Exception ex)
                {
                    Service.Log.Error(ex, $"Exception while converting JToken to type {typeof(T).Name}");
                    return default;
                }
            }

            Service.Log.Error($"Key '{key}' not found in the config file.");
            return default;
        }
        catch (Exception ex)
        {
            Service.Log.Error(ex, $"Failed to get config for {moduleBase.GetType().Name}");
            return default;
        }
    }

    protected static bool AddConfig(DailyModuleBase moduleBase, string key, object? config)
    {
        try
        {
            var configDirectory = P.PluginInterface.GetPluginConfigDirectory();
            var configFile = Path.Combine(configDirectory, moduleBase.GetType().Name + ".json");

            Dictionary<string, object>? existingConfig;

            if (File.Exists(configFile))
            {
                var existingJson = File.ReadAllText(configFile);
                existingConfig = JsonConvert.DeserializeObject<Dictionary<string, object>>(existingJson);
                if (existingConfig != null && existingConfig.ContainsKey(key)) return false;
            }
            else
                existingConfig = [];

            existingConfig ??= [];
            existingConfig[key] = config;

            var jsonString = JsonConvert.SerializeObject(existingConfig, Formatting.Indented);

            File.WriteAllText(configFile, jsonString);

            return true;
        }
        catch (Exception ex)
        {
            Service.Log.Error(ex, $"Failed to write config for {moduleBase.GetType().Name}");
            return false;
        }
    }

    protected static bool UpdateConfig(DailyModuleBase moduleBase, string key, object? newConfig)
    {
        try
        {
            var configDirectory = P.PluginInterface.GetPluginConfigDirectory();
            var configFile = Path.Combine(configDirectory, moduleBase.GetType().Name + ".json");

            if (!File.Exists(configFile))
            {
                Service.Log.Error($"Config file for {moduleBase.GetType().Name} does not exist.");
                return false;
            }

            var existingJson = File.ReadAllText(configFile);
            var existingConfig = JsonConvert.DeserializeObject<Dictionary<string, object?>>(existingJson);

            if (!existingConfig.ContainsKey(key))
            {
                Service.Log.Error($"Key '{key}' does not exist in the config for {moduleBase.GetType().Name}.");
                return false;
            }

            existingConfig[key] = newConfig;

            var jsonString = JsonConvert.SerializeObject(existingConfig, Formatting.Indented);
            File.WriteAllText(configFile, jsonString);

            return true;
        }
        catch (Exception ex)
        {
            Service.Log.Error(ex, $"Failed to update config for {moduleBase.GetType().Name}");
            return false;
        }
    }

    protected static bool RemoveConfig(DailyModuleBase moduleBase, string key)
    {
        try
        {
            var configDirectory = P.PluginInterface.GetPluginConfigDirectory();
            var configFile = Path.Combine(configDirectory, moduleBase.GetType().Name + ".json");

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
            Service.Log.Error(ex, $"Failed to remove config for {moduleBase.GetType().Name}");
            return false;
        }
    }

    protected bool InterruptByConflictKey()
    {
        if (Service.KeyState[Service.Config.ConflictKey])
        {
            TaskManager?.Abort();
            P.PluginInterface.UiBuilder.AddNotification(Service.Lang.GetText("ConflictKey-InterruptMessage"),
                                                        "Daily Routines", NotificationType.Success);
            return true;
        }

        return false;
    }

    public virtual void Uninit()
    {
        if (Overlay != null && P.WindowSystem.Windows.Contains(Overlay)) P.WindowSystem.RemoveWindow(Overlay);
        Overlay = null;

        TaskManager?.Abort();
        TaskManager = null;

        foreach (var field in GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(Hook<>))
            {
                var hookInstance = field.GetValue(this);
                hookInstance?.GetType().GetMethod("Dispose")?.Invoke(hookInstance, null);
            }
        }
    }
}
