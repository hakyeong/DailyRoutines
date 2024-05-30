using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Dalamud.Plugin.Services;

namespace DailyRoutines.Managers;

public class FrameworkManager : IDailyManager
{
    private static Dictionary<string, IFramework.OnUpdateDelegate>? MethodsInfo;
    private static IFramework.OnUpdateDelegate[]? _updateMehtods;
    private static int _length;

    internal static CancellationTokenSource CancelSource { get; } = new();

    private void Init()
    {
        MethodsInfo ??= [];
        _updateMehtods ??= [];

        Service.Framework.Update += DailyRoutines_OnUpdate;
    }

    public bool Register(params IFramework.OnUpdateDelegate[] methods)
    {
        var state = true;
        foreach (var method in methods)
        {
            var uniqueName = GetUniqueName(method);
            if (!MethodsInfo.TryAdd(uniqueName, method)) state = false;
        }

        UpdateMethodsArray();
        return state;
    }

    public bool Unregister(params IFramework.OnUpdateDelegate[] methods)
    {
        var state = true;
        foreach (var method in methods)
        {
            var uniqueName = GetUniqueName(method);
            if (!MethodsInfo.Remove(uniqueName)) state = false;
        }

        UpdateMethodsArray();
        return state;
    }

    public bool Unregister(params MethodInfo[] methods)
    {
        var state = true;
        foreach (var method in methods)
        {
            var uniqueName = GetUniqueName(method);
            if (!MethodsInfo.Remove(uniqueName)) state = false;
        }

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

    private static void DailyRoutines_OnUpdate(IFramework framework)
    {
        for (var i = 0; i < _length; i++)
        {
            var index = i;
            framework.Run(() => _updateMehtods[index].Invoke(framework), CancelSource.Token);
        }
    }

    private void Uninit()
    {
        Service.Framework.Update -= DailyRoutines_OnUpdate;
        CancelSource.Cancel();
        CancelSource.Dispose();

        _updateMehtods = null;
        MethodsInfo = null;
        _length = 0;
    }
}
