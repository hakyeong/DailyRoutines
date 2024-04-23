global using static ECommons.GenericHelpers;
global using static DailyRoutines.Infos.Widgets;
global using static OmenTools.Helpers.HelpersOm;
global using OmenTools.ImGuiOm;
global using OmenTools.Helpers;
using DailyRoutines.Managers;
using Dalamud.Plugin;
using ECommons;

namespace DailyRoutines;

public sealed class Plugin : IDalamudPlugin
{
    public static string Name => "Daily Routines";

    private static bool IsDev;

    public Plugin(DalamudPluginInterface pluginInterface)
    {
        // if (pluginInterface.IsDev)
        // {
        //     IsDev = true;
        //     return;
        // }

        ECommonsMain.Init(pluginInterface, this, Module.DalamudReflector);
        Service.Init(pluginInterface);
    }

    public void Dispose()
    {
        if (IsDev) return;
        ECommonsMain.Dispose();
        Service.Uninit();
    }
}
