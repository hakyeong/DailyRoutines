using DailyRoutines.Infos;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using System.Collections.Generic;
using System.Reflection;
using DailyRoutines.Windows;

namespace DailyRoutines.Managers;

public class ExecuteCommandManager : IDailyManager
{
    private delegate nint ExecuteCommandDelegate(int command, int param1, int param2, int param3, int param4);
    [Signature("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 8B E9 41 8B D9 48 8B 0D ?? ?? ?? ?? 41 8B F8 8B F2", DetourName = nameof(ExecuteCommandDetour))]
    private static Hook<ExecuteCommandDelegate>? ExecuteCommandHook;

    public delegate void ExecuteCommandReceivedDelegate(int command, int param1, int param2, int param3, int param4);

    private static Dictionary<string, ExecuteCommandReceivedDelegate>? MethodsInfo;
    private static ExecuteCommandReceivedDelegate[]? _methods;
    private static int _length;

    private void Init()
    {
        MethodsInfo ??= [];
        _methods ??= [];

        Service.Hook.InitializeFromAttributes(this);
        ExecuteCommandHook?.Enable();
    }

    public nint ExecuteCommand(int command, int param1, int param2, int param3, int param4)
    {
        var result = ExecuteCommandHook.Original(command, param1, param2, param3, param4);
        return result;
    }

    private static nint ExecuteCommandDetour(int command, int param1, int param2, int param3, int param4)
    {
        var original = ExecuteCommandHook.Original(command, param1, param2, param3, param4);

        if (Debug.DebugConfig.ShowExecuteCommandLog)
            Service.Log.Debug($"[ExecuteCommand Manager]\n命令:{command} | p1:{param1} | p2:{param2} | p3:{param3} | p4:{param4}");

        OnExecuteCommand(command, param1, param2, param3, param4);
        return original;
    }

    private static void OnExecuteCommand(int command, int param1, int param2, int param3, int param4)
    {
        for (var i = 0; i < _length; i++)
        {
            _methods[i].Invoke(command, param1, param2, param3, param4);
        }
    }

    public bool Register(params ExecuteCommandReceivedDelegate[] methods)
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

    public bool Unregister(params ExecuteCommandReceivedDelegate[] methods)
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

    private static string GetUniqueName(ExecuteCommandReceivedDelegate method)
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
        _methods = [.. MethodsInfo.Values];
        _length = _methods.Length;
    }

    private void Uninit()
    {
        ExecuteCommandHook?.Dispose();
        ExecuteCommandHook = null;
    }
}
