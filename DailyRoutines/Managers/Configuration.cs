using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Configuration;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Plugin;

namespace DailyRoutines.Managers;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int                      Version                     { get; set; } = 0;
    public VirtualKey               ConflictKey                 { get; set; } = VirtualKey.SHIFT;
    public bool                     SendCalendarToChatWhenLogin { get; set; } = false;
    public bool                     IsHideOutdatedEvent         { get; set; } = true;
    public bool                     AllowAnonymousUpload        { get; set; } = true;
    public int                      DefaultHomePage             { get; set; } = 0;
    public Dictionary<string, bool> ModuleEnabled               { get; set; } = [];
    public HashSet<string>          ModuleFavorites             { get; set; } = [];

    [NonSerialized]
    private DalamudPluginInterface? pluginInterface;

    public void Initialize(DalamudPluginInterface pInterface)
    {
        pluginInterface = pInterface;

        CheckConflictKeyValidation();

        Save();
    }

    private void CheckConflictKeyValidation()
    {
        var validKeys = Service.KeyState.GetValidVirtualKeys();
        if (!validKeys.Contains(ConflictKey))
        {
            ConflictKey = VirtualKey.SHIFT;
            Save();
        }
    }

    public void Save() => pluginInterface!.SavePluginConfig(this);

    public void Uninit() => Save();
}
