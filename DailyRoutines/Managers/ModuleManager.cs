using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DailyRoutines.Infos;
using DailyRoutines.Modules;

namespace DailyRoutines.Managers;

public class ModuleManager : IDailyManager
{
    public Dictionary<Type, DailyModuleBase> Modules { get; private set; } = [];

    private void Init()
    {
        var types = Assembly.GetExecutingAssembly().GetTypes()
                            .Where(t => typeof(DailyModuleBase).IsAssignableFrom(t) &&
                                        !t.IsAbstract &&
                                        t.GetConstructor(Type.EmptyTypes) != null);

        foreach (var type in types)
        {
            var instance = Activator.CreateInstance(type);
            if (instance is DailyModuleBase module)
            {
                Modules.Add(type, module);
                var moduleName = module.GetType().Name;

                Service.Config.ModuleEnabled.TryAdd(moduleName, false);
                if (Service.Config.ModuleEnabled.TryGetValue(moduleName, out var enabled) && !enabled) continue;

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

    public void Unload(DailyModuleBase module)
    {
        if (Modules.ContainsValue(module))
        {
            try
            {
                module.Uninit();
                module.Initialized = false;
                Service.Log.Debug($"Unloaded {module.GetType().Name} moduleBase");
            }
            catch (Exception ex)
            {
                Service.Log.Error(ex, $"Fail to unload {module.GetType().Name} moduleBase");
            }
        }
    }

    public bool IsModuleEnabled(Type moduleType)
        => Modules.TryGetValue(moduleType, out var module) && module.Initialized;

    public bool TryGetModule<T>(out T? module) where T : DailyModuleBase
    {
        var state = Modules.TryGetValue(typeof(T), out var moduleBase);
        module = (T?)moduleBase;
        return state;
    }

    private void Uninit()
    {
        foreach (var module in Modules.Values)
            try
            {
                if (module.Initialized)
                {
                    module.Uninit();
                    module.Initialized = false;
                }
            }
            catch (Exception ex)
            {
                Service.Log.Error(ex, $"Fail to unload {module.GetType().Name} module");
            }

        Modules.Clear();
    }
}
