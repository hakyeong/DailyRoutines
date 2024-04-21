using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DailyRoutines.Notifications;

namespace DailyRoutines.Managers;

public class NotifyManager
{
    public Dictionary<Type, DailyNotificationBase> Engines = [];

    public void Init()
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

    public void Uninit()
    {
        foreach (var engine in Engines.Values)
        {
            engine.Uninit();
            engine.Initialized = false;
        }
    }
}
