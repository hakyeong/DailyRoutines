using System.Linq;
using DailyRoutines.Managers;
using ECommons.Reflection;

namespace DailyRoutines.Infos;

public static unsafe class Utils
{
    public static bool HasPlugin(string name)
    {
        return Service.PluginInterface.InstalledPlugins.FirstOrDefault(x => x.InternalName == name) != null;
    }

    public static bool PluginLoadState(string name)
    {
        return Service.PluginInterface.InstalledPlugins.FirstOrDefault(x => x.InternalName == name)?.IsLoaded ?? false;
    }

    public static bool HasAndEnablePlugin(string name)
    {
        return HasPlugin(name) && PluginLoadState(name);
    }
}
