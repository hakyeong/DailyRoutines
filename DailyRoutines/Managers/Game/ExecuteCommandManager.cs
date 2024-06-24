using DailyRoutines.Infos;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using System.Collections.Generic;
using DailyRoutines.Windows;

namespace DailyRoutines.Managers;

public class ExecuteCommandManager : IDailyManager
{
    private delegate nint ExecuteCommandDelegate(int command, int param1, int param2, int param3, int param4);
    [Signature("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 8B E9 41 8B D9 48 8B 0D ?? ?? ?? ?? 41 8B F8 8B F2", DetourName = nameof(ExecuteCommandDetour))]
    private static Hook<ExecuteCommandDelegate>? ExecuteCommandHook;

    public delegate void PreExecuteCommandDelegate(ref bool isPrevented, ref int command, ref int param1, ref int param2, ref int param3, 
                                                   ref int param4);
    public delegate void ExecuteCommandReceivedDelegate(int command, int param1, int param2, int param3, int param4);

    private static Dictionary<string, ExecuteCommandReceivedDelegate>? MethodsInfoReceive;
    private static ExecuteCommandReceivedDelegate[]? _methodsReceive;
    private static int _lengthReceive;

    private static Dictionary<string, PreExecuteCommandDelegate>? MethodsInfoPre;
    private static PreExecuteCommandDelegate[]? _methodsPre;
    private static int _lengthPre;

    private void Init()
    {
        MethodsInfoReceive ??= [];
        _methodsReceive ??= [];

        MethodsInfoPre ??= [];
        _methodsPre ??= [];

        Service.Hook.InitializeFromAttributes(this);
        ExecuteCommandHook?.Enable();
    }

    public nint ExecuteCommand(int command, int param1 = 0, int param2 = 0, int param3 = 0, int param4 = 0)
    {
        var result = ExecuteCommandHook.Original(command, param1, param2, param3, param4);
        return result;
    }

    public nint ExecuteCommand(ExecuteCommandFlag command, int param1 = 0, int param2 = 0, int param3 = 0, int param4 = 0)
    {
        var result = ExecuteCommandHook.Original((int)command, param1, param2, param3, param4);
        return result;
    }

    private static nint ExecuteCommandDetour(int command, int param1, int param2, int param3, int param4)
    {
        if (Debug.DebugConfig.ShowExecuteCommandLog)
            Service.Log.Debug($"[Execute Command Manager]\n命令:{(ExecuteCommandFlag)command}({command}) | p1:{param1} | p2:{param2} | p3:{param3} | p4:{param4}");

        var isPrevented = false;
        for (var i = 0; i < _lengthPre; i++)
        {
            _methodsPre[i].Invoke(ref isPrevented, ref command, ref param1, ref param2, ref param3, ref param4);
        }

        if (isPrevented) return 0;

        var original = ExecuteCommandHook.Original(command, param1, param2, param3, param4);

        for (var i = 0; i < _lengthReceive; i++)
        {
            _methodsReceive[i].Invoke(command, param1, param2, param3, param4);
        }

        return original;
    }

    public bool Register(params ExecuteCommandReceivedDelegate[] methods)
    {
        var state = true;
        foreach (var method in methods)
        {
            var uniqueName = GetUniqueName(method);
            if (!MethodsInfoReceive.TryAdd(uniqueName, method)) state = false;
        }

        UpdateMethodsArray();
        return state;
    }

    public bool Register(params PreExecuteCommandDelegate[] methods)
    {
        var state = true;
        foreach (var method in methods)
        {
            var uniqueName = GetUniqueName(method);
            if (!MethodsInfoPre.TryAdd(uniqueName, method)) state = false;
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
            if (!MethodsInfoReceive.Remove(uniqueName)) state = false;
        }

        UpdateMethodsArray();
        return state;
    }

    public bool Unregister(params PreExecuteCommandDelegate[] methods)
    {
        var state = true;
        foreach (var method in methods)
        {
            var uniqueName = GetUniqueName(method);
            if (!MethodsInfoPre.Remove(uniqueName)) state = false;
        }

        UpdateMethodsArray();
        return state;
    }

    private static string GetUniqueName(ExecuteCommandReceivedDelegate method)
    {
        var methodInfo = method.Method;
        return $"{methodInfo.DeclaringType.FullName}_{methodInfo.Name}";
    }

    private static string GetUniqueName(PreExecuteCommandDelegate method)
    {
        var methodInfo = method.Method;
        return $"{methodInfo.DeclaringType.FullName}_{methodInfo.Name}";
    }

    private static void UpdateMethodsArray()
    {
        _methodsReceive = [.. MethodsInfoReceive.Values];
        _lengthReceive = _methodsReceive.Length;
        _methodsPre = [.. MethodsInfoPre.Values];
        _lengthPre = _methodsPre.Length;
    }

    private void Uninit()
    {
        ExecuteCommandHook?.Dispose();
        ExecuteCommandHook = null;
    }
}
