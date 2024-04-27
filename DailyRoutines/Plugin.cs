global using static ECommons.GenericHelpers;
global using static DailyRoutines.Infos.Widgets;
global using static OmenTools.Helpers.HelpersOm;
global using OmenTools.ImGuiOm;
global using OmenTools.Helpers;
using System;
using DailyRoutines.Managers;
using Dalamud.Plugin;
using ECommons;
using System.Reflection;
using Module = ECommons.Module;

namespace DailyRoutines;

public sealed class Plugin : IDalamudPlugin
{
    public static string Name => "Daily Routines";
    public static Version? Version { get; private set; }

    private static bool IsDev;

    public Plugin(DalamudPluginInterface pluginInterface)
    {
        // if (pluginInterface.IsDev)
        // {
        //     IsDev = true;
        //     return;
        // }

        Version ??= Assembly.GetExecutingAssembly().GetName().Version;

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
