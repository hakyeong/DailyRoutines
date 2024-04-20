using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DailyRoutines.Modules;

namespace DailyRoutines.Managers;

public class ModuleManager
{
    public static Dictionary<Type, DailyModuleBase> Modules { get; private set; } = [];

    public ModuleManager()
    {
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

    public void Init()
    {
        foreach (var module in Modules.Values)
        {
            var moduleName = module.GetType().Name;
            if (Service.Config.ModuleEnabled.TryGetValue(moduleName, out var enabled))
            {
                if (!enabled) continue;
            }
            else
            {
                Service.Log.Warning($"Fail to get module {moduleName} configurations, skip loading");
                continue;
            }

            try
            {
                if (!module.Initialized)
                {
                    module.Init();
                    module.Initialized = true;
                }
                else
                    Service.Log.Debug($"{moduleName} has been loaded, skip.");
            }
            catch (Exception ex)
            {
                Service.Log.Error($"Failed to load module {module} due to error: {ex.Message}");
                Service.Log.Error(ex.StackTrace ?? "Unknown");
            }
        }
    }

    public void Load(DailyModuleBase module)
    {
        if (Modules.ContainsValue(module))
        {
            var moduleName = module.GetType().Name;
            try
            {
                if (!module.Initialized)
                {
                    module.Init();
                    module.Initialized = true;
                    Service.Log.Debug($"Loaded {moduleName}");
                }
                else
                    Service.Log.Debug($"{moduleName} has been loaded, skip.");
            }
            catch (Exception ex)
            {
                Service.Log.Error($"Failed to load {moduleName} due to error: {ex.Message}");
                Service.Log.Error(ex.StackTrace ?? "Unknown");
            }
        }
        else
            Service.Log.Error($"Fail to fetch {module}");
    }

    public void Unload(DailyModuleBase component)
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

    public void Uninit()
    {
        foreach (var component in Modules.Values)
            try
            {
                if (component.Initialized)
                {
                    component.Uninit();
                    component.Initialized = false;
                }
            }
            catch (Exception ex)
            {
                Service.Log.Error(ex, $"Fail to unload {component.GetType().Name} moduleBase");
            }
    }
}
