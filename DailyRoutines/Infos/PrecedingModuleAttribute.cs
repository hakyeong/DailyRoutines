using System;

namespace DailyRoutines.Infos;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class PrecedingModuleAttribute(Type[] modules) : Attribute
{
    public Type[] Modules { get; } = modules;
}
