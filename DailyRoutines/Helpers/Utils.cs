using System.Linq;
using DailyRoutines.Managers;

namespace DailyRoutines.Helpers;

public static class Utils
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

    public static bool IsChineseString(string text)
    {
        return text.All(IsChineseCharacter);
    }

    public static bool IsChineseCharacter(char c)
    {
        return (c >= 0x4E00 && c <= 0x9FA5) || (c >= 0x3400 && c <= 0x4DB5);
    }
}
