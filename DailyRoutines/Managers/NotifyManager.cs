using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DailyRoutines.Infos;
using DailyRoutines.Notifications;

namespace DailyRoutines.Managers;

public class NotifyManager : IDailyManager
{
    public Dictionary<Type, DailyNotificationBase> Engines = [];

    private void Init()
    {
        var types = Assembly.GetExecutingAssembly().GetTypes()
                            .Where(t => typeof(DailyNotificationBase).IsAssignableFrom(t) &&
                                        !t.IsAbstract &&
                                        t.GetConstructor(Type.EmptyTypes) != null);

        foreach (var type in types)
        {
            var instance = Activator.CreateInstance(type);
            if (instance is DailyNotificationBase engine)
            {
                Engines.Add(type, engine);

                engine.Init();
                engine.Initialized = true;
            }
        }
    }

    private void Uninit()
    {
        foreach (var engine in Engines.Values)
        {
            engine.Uninit();
            engine.Initialized = false;
        }
    }
}
