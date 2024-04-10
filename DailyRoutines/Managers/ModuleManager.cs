using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ClickLib;
using DailyRoutines.Modules;

namespace DailyRoutines.Managers;

public class ModuleManager
{
    public static Dictionary<Type, DailyModuleBase> Modules { get; private set; } = [];

    public ModuleManager()
    {
        Click.Initialize();

        var types = Assembly.GetExecutingAssembly().GetTypes()
                            .Where(t => typeof(DailyModuleBase).IsAssignableFrom(t) &&
                                        !t.IsAbstract &&
                                        t.GetConstructor(Type.EmptyTypes) != null);

        foreach (var type in types)
        {
            var instance = Activator.CreateInstance(type);
            if (instance is DailyModuleBase component)
                Modules.Add(type, component);
        }
    }

    public static void Init()
    {
        foreach (var component in Modules.Values)
        {
            if (Service.Config.ModuleEnabled.TryGetValue(component.GetType().Name, out var enabled))
            {
                if (!enabled)
                    continue;
            }
            else
            {
                Service.Log.Warning($"Fail to get moduleBase {component.GetType().Name} configurations, skip loading");
                continue;
            }

            try
            {
                if (!component.Initialized)
                {
                    component.Init();
                    component.Initialized = true;
                    Service.Log.Debug($"Loaded {component.GetType().Name} moduleBase");
                }
                else
                    Service.Log.Debug($"{component.GetType().Name} has been loaded, skip.");
            }
            catch (Exception ex)
            {
                Service.Log.Error($"Failed to load moduleBase {component.GetType().Name} due to error: {ex.Message}");
                Service.Log.Error(ex.StackTrace ?? "Unknown");
            }
        }
    }

    public static void Load(DailyModuleBase component)
    {
        if (Modules.ContainsValue(component))
        {
            try
            {
                if (!component.Initialized)
                {
                    component.Init();
                    component.Initialized = true;
                    Service.Log.Debug($"Loaded {component.GetType().Name} moduleBase");
                }
                else
                    Service.Log.Debug($"{component.GetType().Name} has been loaded, skip.");
            }
            catch (Exception ex)
            {
                Service.Log.Error($"Failed to load component {component.GetType().Name} due to error: {ex.Message}");
                Service.Log.Error(ex.StackTrace ?? "Unknown");
            }
        }
        else
            Service.Log.Error($"Fail to fetch component {component}");
    }

    public static void Unload(DailyModuleBase component)
    {
        if (Modules.ContainsValue(component))
        {
            try
            {
                component.Uninit();
                component.Initialized = false;
                Service.Log.Debug($"Unloaded {component.GetType().Name} moduleBase");
            }
            catch (Exception ex)
            {
                Service.Log.Error(ex, $"Fail to unload {component.GetType().Name} moduleBase");
            }
        }
    }

    public static void Uninit()
    {
        foreach (var component in Modules.Values)
        {
            try
            {
                if (component.Initialized)
                {
                    component.Uninit();
                    component.Initialized = false;
                    Service.Log.Debug($"Unloaded {component.GetType().Name} moduleBase");
                }
            }
            catch (Exception ex)
            {
                Service.Log.Error(ex, $"Fail to unload {component.GetType().Name} moduleBase");
            }
        }
    }
}
