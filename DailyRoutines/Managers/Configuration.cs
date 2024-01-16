using System;
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

    public string GetConfigPath() => pluginInterface.ConfigFile.FullName;

    public void Uninitialize()
    {

    }
}
