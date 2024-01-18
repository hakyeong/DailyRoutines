using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ClickLib;
using DailyRoutines.Infos;
using DailyRoutines.Managers;

namespace DailyRoutines.Manager;

public class ModuleManager
{
    public static List<IDailyModule> Modules = new();

    public ModuleManager()
    {
        Click.Initialize();

        var types = Assembly.GetExecutingAssembly().GetTypes()
                            .Where(t => t.GetInterfaces().Contains(typeof(IDailyModule)) &&
                                        t.GetConstructor(Type.EmptyTypes) != null);

        foreach (var type in types)
        {
            var instance = Activator.CreateInstance(type);
            if (instance is IDailyModule component) Modules.Add(component);
        }
    }

    public static bool IsModuleLoaded(IDailyModule? module)
    {
        return module is { Initialized: true };
    }


    public static void Init()
    {
        foreach (var component in Modules)
        {
            if (Service.Config.ModuleEnabled.TryGetValue(component.GetType().Name, out var enabled))
            {
                if (!enabled) continue;
            }
            else
            {
                Service.Log.Warning($"Fail to get module {component.GetType().Name} configurations, skip loading");
                continue;
            }

            try
            {
                if (!component.Initialized)
                {
                    component.Init();
                    Service.Log.Debug($"Loaded {component.GetType().Name} module");
                }
                else
                    Service.Log.Debug($"{component.GetType().Name} has been loaded, skip.");
            }
            catch (Exception ex)
            {
                Service.Log.Error($"Failed to load module {component.GetType().Name} due to error: {ex.Message}");
                Service.Log.Warning(ex.StackTrace ?? "Unknown");
            }
        }
    }

    public static void Load(IDailyModule component)
    {
        if (Modules.Contains(component))
        {
            try
            {
                if (!component.Initialized)
                {
                    component.Init();
                    Service.Log.Debug($"Loaded {component.GetType().Name} module");
                }
                else
                    Service.Log.Debug($"{component.GetType().Name} has been loaded, skip.");
            }
            catch (Exception ex)
            {
                Service.Log.Error($"Failed to load component {component.GetType().Name} due to error: {ex.Message}");
            }
        }
        else
            Service.Log.Error($"Fail to fetch component {component}");
    }

    public static void Unload(IDailyModule component)
    {
        if (Modules.Contains(component))
        {
            component.Uninit();
            Service.Log.Debug($"Unloaded {component.GetType().Name} module");
        }
    }

    public static void Uninit()
    {
        foreach (var component in Modules)
        {
            component.Uninit();
            Service.Log.Debug($"Unloaded {component.GetType().Name} module");
        }
    }
}
