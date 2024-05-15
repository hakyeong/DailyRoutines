using DailyRoutines.Infos;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game;
using System.Collections.Generic;
using System.Numerics;

namespace DailyRoutines.Managers;

public unsafe class UseActionManager : IDailyManager
{
    public delegate void PreUseActionDelegate(
        ref ActionType actionType, ref uint actionID, ref ulong targetID, ref uint a4, ref uint queueState, ref uint a6);
    public delegate void PostUseActionDelegate(bool result, ActionType actionType, uint actionID, ulong targetID, uint a4, uint queueState, uint a6);

    public delegate void PreUseActionLocationDelegate(
        ref ActionType type, ref uint actionID, ref ulong targetID, ref Vector3* location, ref uint a4);
    public delegate void PostUseActionLocationDelegate(bool result, ActionType actionType, uint actionID, ulong targetID, Vector3* location, uint a4);

    private delegate bool useActionDelegate(
        ActionManager* actionManager, ActionType actionType, uint actionID, ulong targetID = 0xE000_0000, uint a4 = 0,
        uint queueState = 0, uint a6 = 0, void* a7 = null);
    private static Hook<useActionDelegate>? UseActionHook;

    private delegate bool useActionLocationDelegate(ActionManager* manager, ActionType type, uint actionID, ulong targetID, Vector3* location, uint a4);
    private Hook<useActionLocationDelegate>? UseActionLocationHook;

    // PreUseAction
    private static readonly Dictionary<string, PreUseActionDelegate>? MethodsInfoPreUseAction = [];
    private static PreUseActionDelegate[]? _methodsPreUseAction = [];
    // PostUseAction
    private static readonly Dictionary<string, PostUseActionDelegate>? MethodsInfoPostUseAction = [];
    private static PostUseActionDelegate[]? _methodsPostUseAction = [];

    // PreUseActionLocation
    private static readonly Dictionary<string, PreUseActionLocationDelegate>? MethodsInfoPreUseActionLocation = [];
    private static PreUseActionLocationDelegate[]? _methodsPreUseActionLocation = [];
    // PostUseActionLocation
    private static readonly Dictionary<string, PostUseActionLocationDelegate>? MethodsInfoPostUseActionLocation = [];
    private static PostUseActionLocationDelegate[]? _methodsPostUseActionLocation = [];

    private void Init()
    {
        UseActionHook = Service.Hook.HookFromAddress<useActionDelegate>(
            (nint)ActionManager.MemberFunctionPointers.UseAction, UseActionDetour);
        UseActionHook.Enable();

        UseActionLocationHook = Service.Hook.HookFromAddress<useActionLocationDelegate>((nint)ActionManager.MemberFunctionPointers.UseActionLocation, UseActionLocationDetour);
        UseActionLocationHook?.Enable();
    }

    #region PreUseAction
    public bool Register(params PreUseActionDelegate[] methods)
    {
        var state = true;
        foreach (var method in methods)
        {
            var uniqueName = GetUniqueName(method);
            if (!MethodsInfoPreUseAction.TryAdd(uniqueName, method)) state = false;
        }

        UpdateMethodsArrayPreUseAction();
        return state;
    }

    public bool Unregister(params PreUseActionDelegate[] methods)
    {
        var state = true;
        foreach (var method in methods)
        {
            var uniqueName = GetUniqueName(method);
            if (!MethodsInfoPreUseAction.Remove(uniqueName)) state = false;
        }

        UpdateMethodsArrayPreUseAction();
        return state;
    }

    private static void UpdateMethodsArrayPreUseAction()
    {
        _methodsPreUseAction = [.. MethodsInfoPreUseAction.Values];
    }

    private static string GetUniqueName(PreUseActionDelegate method)
    {
        var methodInfo = method.Method;
        return $"{methodInfo.DeclaringType.FullName}_{methodInfo.Name}";
    }
    #endregion

    #region PostUseAction
    public bool Register(params PostUseActionDelegate[] methods)
    {
        var state = true;
        foreach (var method in methods)
        {
            var uniqueName = GetUniqueName(method);
            if (!MethodsInfoPostUseAction.TryAdd(uniqueName, method)) state = false;
        }

        UpdateMethodsArrayPostUseAction();
        return state;
    }

    public bool Unregister(params PostUseActionDelegate[] methods)
    {
        var state = true;
        foreach (var method in methods)
        {
            var uniqueName = GetUniqueName(method);
            if (!MethodsInfoPostUseAction.Remove(uniqueName)) state = false;
        }

        UpdateMethodsArrayPostUseAction();
        return state;
    }

    private static void UpdateMethodsArrayPostUseAction()
    {
        _methodsPostUseAction = [.. MethodsInfoPostUseAction.Values];
    }

    private static string GetUniqueName(PostUseActionDelegate method)
    {
        var methodInfo = method.Method;
        return $"{methodInfo.DeclaringType.FullName}_{methodInfo.Name}";
    }
    #endregion

    #region PreUseActionLocation
    public bool Register(params PreUseActionLocationDelegate[] methods)
    {
        var state = true;
        foreach (var method in methods)
        {
            var uniqueName = GetUniqueName(method);
            if (!MethodsInfoPreUseActionLocation.TryAdd(uniqueName, method)) state = false;
        }

        UpdateMethodsArrayPreUseActionLocation();
        return state;
    }

    public bool Unregister(params PreUseActionLocationDelegate[] methods)
    {
        var state = true;
        foreach (var method in methods)
        {
            var uniqueName = GetUniqueName(method);
            if (!MethodsInfoPreUseActionLocation.Remove(uniqueName)) state = false;
        }

        UpdateMethodsArrayPreUseActionLocation();
        return state;
    }

    private static void UpdateMethodsArrayPreUseActionLocation()
    {
        _methodsPreUseActionLocation = [.. MethodsInfoPreUseActionLocation.Values];
    }

    private static string GetUniqueName(PreUseActionLocationDelegate method)
    {
        var methodInfo = method.Method;
        return $"{methodInfo.DeclaringType.FullName}_{methodInfo.Name}";
    }
    #endregion

    #region PostUseActionLocation
    public bool Register(params PostUseActionLocationDelegate[] methods)
    {
        var state = true;
        foreach (var method in methods)
        {
            var uniqueName = GetUniqueName(method);
            if (!MethodsInfoPostUseActionLocation.TryAdd(uniqueName, method)) state = false;
        }

        UpdateMethodsArrayPostUseActionLocation();
        return state;
    }

    public bool Unregister(params PostUseActionLocationDelegate[] methods)
    {
        var state = true;
        foreach (var method in methods)
        {
            var uniqueName = GetUniqueName(method);
            if (!MethodsInfoPostUseActionLocation.Remove(uniqueName)) state = false;
        }

        UpdateMethodsArrayPostUseActionLocation();
        return state;
    }

    private static void UpdateMethodsArrayPostUseActionLocation()
    {
        _methodsPostUseActionLocation = [.. MethodsInfoPostUseActionLocation.Values];
    }

    private static string GetUniqueName(PostUseActionLocationDelegate method)
    {
        var methodInfo = method.Method;
        return $"{methodInfo.DeclaringType.FullName}_{methodInfo.Name}";
    }
    #endregion

    #region Hooks
    private static bool UseActionDetour(
        ActionManager* actionManager, ActionType actionType, uint actionID, ulong targetID = 0xE000_0000, uint a4 = 0,
        uint queueState = 0, uint a6 = 0, void* a7 = null)
    {
        foreach (var preUseAction in _methodsPreUseAction)
        {
            preUseAction.Invoke(ref actionType, ref actionID, ref targetID, ref a4, ref queueState, ref a6);
        }

        var original = UseActionHook.Original(actionManager, actionType, actionID, targetID, a4, queueState, a6, a7);
        foreach (var postUseAction in _methodsPostUseAction)
        {
            postUseAction.Invoke(original, actionType, actionID, targetID, a4, queueState, a6);
        }

        return original;
    }

    private bool UseActionLocationDetour(
        ActionManager* manager, ActionType type, uint actionID, ulong targetID, Vector3* location, uint a4)
    {
        foreach (var preUseAction in _methodsPreUseActionLocation)
        {
            preUseAction.Invoke(ref type, ref actionID, ref targetID, ref location, ref a4);
        }

        var original = UseActionLocationHook.Original(manager, type, actionID, targetID, location, a4);
        foreach (var postUseAction in _methodsPostUseActionLocation)
        {
            postUseAction.Invoke(original, type, actionID, targetID, location, a4);
        }

        return original;
    }
    #endregion

    private void Uninit()
    {
        UseActionHook?.Dispose();
        UseActionHook = null;

        UseActionLocationHook?.Dispose();
        UseActionLocationHook = null;
    }
}
