using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DailyRoutines.Modules;
using Dalamud.Configuration;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Plugin;

namespace DailyRoutines.Managers;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;
    public string SelectedLanguage { get; set; } = string.Empty;
    public VirtualKey ConflictKey { get; set; } = VirtualKey.SHIFT;
    public Dictionary<string, bool> ModuleEnabled { get; set; } = [];

    [NonSerialized]
    private DalamudPluginInterface? pluginInterface;

    public void Initialize(DalamudPluginInterface pInterface)
    {
        pluginInterface = pInterface;

        var assembly = Assembly.GetExecutingAssembly();
        var moduleTypes = assembly.GetTypes()
                                  .Where(t => typeof(DailyModuleBase).IsAssignableFrom(t) &&
                                              t is { IsClass: true, IsAbstract: false });
        foreach (var module in moduleTypes) ModuleEnabled.TryAdd(module.Name, false);
        Save();
    }

    public void Save() => pluginInterface!.SavePluginConfig(this);

    public void Uninit() => Save();
}
