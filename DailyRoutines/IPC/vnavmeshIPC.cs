using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using DailyRoutines.Helpers;
using DailyRoutines.Managers;
using Dalamud.Plugin.Ipc;

namespace DailyRoutines.IPC;

internal class vnavmeshIPC : DailyIPCBase
{
    public override string? InternalName { get; set; } = "vnavmesh";

    private static ICallGateSubscriber<bool>? _navIsReady;
    private static ICallGateSubscriber<float>? _navBuildProgress;
    private static ICallGateSubscriber<bool>? _navReload;
    private static ICallGateSubscriber<bool>? _navRebuild;
    private static ICallGateSubscriber<Vector3, Vector3, bool, Task<List<Vector3>>>? _navPathfind;
    private static ICallGateSubscriber<bool>? _navIsAutoLoad;
    private static ICallGateSubscriber<bool, object>? _navSetAutoLoad;

    private static ICallGateSubscriber<Vector3, float, float, Vector3?>? _queryMeshNearestPoint;
    private static ICallGateSubscriber<Vector3, float, Vector3?>? _queryMeshPointOnFloor;

    private static ICallGateSubscriber<List<Vector3>, bool, object>? _pathMoveTo;
    private static ICallGateSubscriber<object>? _pathStop;
    private static ICallGateSubscriber<bool>? _pathIsRunning;
    private static ICallGateSubscriber<int>? _pathNumWaypoints;
    private static ICallGateSubscriber<bool>? _pathGetMovementAllowed;
    private static ICallGateSubscriber<bool, object>? _pathSetMovementAllowed;
    private static ICallGateSubscriber<bool>? _pathGetAlignCamera;
    private static ICallGateSubscriber<bool, object>? _pathSetAlignCamera;
    private static ICallGateSubscriber<float>? _pathGetTolerance;
    private static ICallGateSubscriber<float, object>? _pathSetTolerance;

    private static ICallGateSubscriber<Vector3, bool, bool>? _pathfindAndMoveTo;
    private static ICallGateSubscriber<bool>? _pathfindInProgress;

    public override void Init()
    {
        if (!IPCManager.IsPluginEnabled(InternalName)) return;

        try
        {
            _navIsReady = PI.GetIpcSubscriber<bool>($"{InternalName}.Nav.IsReady");
            _navBuildProgress = PI.GetIpcSubscriber<float>($"{InternalName}.Nav.BuildProgress");
            _navReload = PI.GetIpcSubscriber<bool>($"{InternalName}.Nav.Reload");
            _navRebuild = PI.GetIpcSubscriber<bool>($"{InternalName}.Nav.Rebuild");
            _navPathfind = PI.GetIpcSubscriber<Vector3, Vector3, bool, Task<List<Vector3>>>(
                    $"{InternalName}.Nav.Pathfind");
            _navIsAutoLoad = PI.GetIpcSubscriber<bool>($"{InternalName}.Nav.IsAutoLoad");
            _navSetAutoLoad = PI.GetIpcSubscriber<bool, object>($"{InternalName}.Nav.SetAutoLoad");

            _queryMeshNearestPoint = PI.GetIpcSubscriber<Vector3, float, float, Vector3?>($"{InternalName}.Query.Mesh.NearestPoint");
            _queryMeshPointOnFloor = PI.GetIpcSubscriber<Vector3, float, Vector3?>($"{InternalName}.Query.Mesh.PointOnFloor");

            _pathMoveTo = PI.GetIpcSubscriber<List<Vector3>, bool, object>($"{InternalName}.Path.MoveTo");
            _pathStop = PI.GetIpcSubscriber<object>($"{InternalName}.Path.Stop");
            _pathIsRunning = PI.GetIpcSubscriber<bool>($"{InternalName}.Path.IsRunning");
            _pathNumWaypoints = PI.GetIpcSubscriber<int>($"{InternalName}.Path.NumWaypoints");
            _pathGetMovementAllowed = PI.GetIpcSubscriber<bool>($"{InternalName}.Path.GetMovementAllowed");
            _pathSetMovementAllowed =
                PI.GetIpcSubscriber<bool, object>($"{InternalName}.Path.SetMovementAllowed");
            _pathGetAlignCamera = PI.GetIpcSubscriber<bool>($"{InternalName}.Path.GetAlignCamera");
            _pathSetAlignCamera = PI.GetIpcSubscriber<bool, object>($"{InternalName}.Path.SetAlignCamera");
            _pathGetTolerance = PI.GetIpcSubscriber<float>($"{InternalName}.Path.GetTolerance");
            _pathSetTolerance = PI.GetIpcSubscriber<float, object>($"{InternalName}.Path.SetTolerance");

            _pathfindAndMoveTo =
                PI.GetIpcSubscriber<Vector3, bool, bool>($"{InternalName}.SimpleMove.PathfindAndMoveTo");
            _pathfindInProgress =
                PI.GetIpcSubscriber<bool>($"{InternalName}.SimpleMove.PathfindInProgress");
        }
        catch (Exception ex)
        {
            ex.Log();
        }
    }

    internal T? Execute<T>(Func<T>? func)
    {
        if (!IPCManager.IsPluginEnabled(InternalName)) return default;

        try
        {
            if (func != null) return func();
        }
        catch (Exception ex)
        {
            ex.Log();
        }

        return default;
    }

    internal void Execute(Action action)
    {
        if (IPCManager.IsPluginEnabled(InternalName))
        {
            try
            {
                action?.Invoke();
            }
            catch (Exception ex)
            {
                ex.Log();
            }
        }
    }

    internal void Execute<T>(Action<T>? action, T param)
    {
        if (!IPCManager.IsPluginEnabled(InternalName)) return;

        try
        {
            action?.Invoke(param);
        }
        catch (Exception ex)
        {
            ex.Log();
        }
    }

    internal void Execute<T1, T2>(Action<T1, T2>? action, T1 p1, T2 p2)
    {
        if (!IPCManager.IsPluginEnabled(InternalName)) return;

        try
        {
            action?.Invoke(p1, p2);
        }
        catch (Exception ex)
        {
            ex.Log();
        }
    }

    internal bool NavIsReady()
    {
        return Execute(() => _navIsReady!.InvokeFunc());
    }

    internal float NavBuildProgress()
    {
        return Execute(() => _navBuildProgress!.InvokeFunc());
    }

    internal void NavReload()
    {
        Execute(() => _navReload!.InvokeFunc());
    }

    internal void NavRebuild()
    {
        Execute(() => _navRebuild!.InvokeFunc());
    }

    internal Task<List<Vector3>>? NavPathfind(Vector3 from, Vector3 to, bool fly = false)
    {
        return Execute(() => _navPathfind!.InvokeFunc(from, to, fly));
    }

    internal bool NavIsAutoLoad()
    {
        return Execute(() => _navIsAutoLoad!.InvokeFunc());
    }

    internal void NavSetAutoLoad(bool value)
    {
        Execute(_navSetAutoLoad!.InvokeAction, value);
    }

    internal Vector3? QueryMeshNearestPoint(Vector3 pos, float halfExtentXZ, float halfExtentY)
    {
        return Execute(() => _queryMeshNearestPoint!.InvokeFunc(pos, halfExtentXZ, halfExtentY));
    }

    internal Vector3? QueryMeshPointOnFloor(Vector3 pos, float halfExtentXZ)
    {
        return Execute(() => _queryMeshPointOnFloor!.InvokeFunc(pos, halfExtentXZ));
    }

    internal void PathMoveTo(List<Vector3> waypoints, bool fly)
    {
        Execute(_pathMoveTo!.InvokeAction, waypoints, fly);
    }

    internal void PathStop()
    {
        Execute(_pathStop!.InvokeAction);
    }

    internal bool PathIsRunning()
    {
        return Execute(() => _pathIsRunning!.InvokeFunc());
    }

    internal int PathNumWaypoints()
    {
        return Execute(() => _pathNumWaypoints!.InvokeFunc());
    }

    internal bool PathGetMovementAllowed()
    {
        return Execute(() => _pathGetMovementAllowed!.InvokeFunc());
    }

    internal void PathSetMovementAllowed(bool value)
    {
        Execute(_pathSetMovementAllowed!.InvokeAction, value);
    }

    internal bool PathGetAlignCamera()
    {
        return Execute(() => _pathGetAlignCamera!.InvokeFunc());
    }

    internal void PathSetAlignCamera(bool value)
    {
        Execute(_pathSetAlignCamera!.InvokeAction, value);
    }

    internal float PathGetTolerance()
    {
        return Execute(() => _pathGetTolerance!.InvokeFunc());
    }

    internal void PathSetTolerance(float tolerance)
    {
        Execute(_pathSetTolerance!.InvokeAction, tolerance);
    }

    internal void PathfindAndMoveTo(Vector3 pos, bool fly)
    {
        Execute(() => _pathfindAndMoveTo!.InvokeFunc(pos, fly));
    }

    internal bool PathfindInProgress()
    {
        return Execute(() => _pathfindInProgress!.InvokeFunc());
    }
}
