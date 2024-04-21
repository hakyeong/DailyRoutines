using System.Collections.Generic;
using System.Reflection;
using Dalamud.Plugin.Services;

namespace DailyRoutines.Managers;

public class FrameworkManager
{
    internal static Dictionary<string, IFramework.OnUpdateDelegate>? MethodsInfo;
    private static IFramework.OnUpdateDelegate[]? _updateMehtods;
    internal static int _length;

    public void Init()
    {
        MethodsInfo ??= [];
        _updateMehtods ??= [];

        Service.Framework.Update += OnUpdate;
    }

    public bool Register(IFramework.OnUpdateDelegate method)
    {
        var uniqueName = GetUniqueName(method);
        if (!MethodsInfo.TryAdd(uniqueName, method)) return false;

        UpdateMethodsArray();
        return true;
    }

    public bool Register(params IFramework.OnUpdateDelegate[] methods)
    {
        var state = true;
        foreach (var method in methods)
            if (!Register(method)) state = false;

        UpdateMethodsArray();
        return state;
    }

    public bool Unregister(IFramework.OnUpdateDelegate method)
    {
        var uniqueName = GetUniqueName(method);
        if (!MethodsInfo.Remove(uniqueName)) return false;

        UpdateMethodsArray();
        return true;
    }

    public bool Unregister(params IFramework.OnUpdateDelegate[] methods)
    {
        var state = true;
        foreach (var method in methods)
            if (!Unregister(method)) state = false;

        UpdateMethodsArray();
        return state;
    }

    public bool Unregister(MethodInfo method)
    {
        var uniqueName = GetUniqueName(method);
        if (!MethodsInfo.Remove(uniqueName)) return false;

        UpdateMethodsArray();
        return true;
    }

    public bool Unregister(params MethodInfo[] methods)
    {
        var state = true;
        foreach (var method in methods)
            if (!Unregister(method)) state = false;

        UpdateMethodsArray();
        return state;
    }

    private static string GetUniqueName(IFramework.OnUpdateDelegate method)
    {
        var methodInfo = method.Method;
        return $"{methodInfo.DeclaringType.FullName}_{methodInfo.Name}";
    }

    private static string GetUniqueName(MemberInfo methodInfo)
    {
        return $"{methodInfo.DeclaringType.FullName}_{methodInfo.Name}";
    }

    private static void UpdateMethodsArray()
    {
        _updateMehtods = [..MethodsInfo.Values];
        _length = _updateMehtods.Length;
    }

    private static void OnUpdate(IFramework framework)
    {
        for (var i = 0; i < _length; i++)
        {
            var method = _updateMehtods[i];
            method.Invoke(framework);
        }
    }

    public void Uninit()
    {
        Service.Framework.Update -= OnUpdate;

        _updateMehtods = null;
        MethodsInfo = null;
        _length = 0;
    }
}
