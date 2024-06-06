using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DailyRoutines.Managers;

namespace DailyRoutines.Helpers;

public partial class TaskHelper : IDisposable
{
    private static readonly List<TaskHelper> Instances = [];
    private FrameThrottler<string> FrameThrottler = new();
    private Throttler<string> Throttler = new();

    public TaskHelper()
    {
        Service.Framework.Update += Tick;
        Instances.Add(this);
    }

    private TaskHelperTask?      CurrentTask { get; set; }
    public  string               CurrentTaskName => CurrentTask?.Name;
    private List<TaskHelperTask> Tasks { get; set; } = [];
    private List<TaskHelperTask> ImmediateTasks { get; set; } = [];
    public  List<string>         TaskStack => ImmediateTasks.Select(x => x.Name).Union(Tasks.Select(x => x.Name)).ToList();
    public  int                  NumQueuedTasks => Tasks.Count + ImmediateTasks.Count + (CurrentTask == null ? 0 : 1);
    public  bool                 IsBusy => CurrentTask != null || Tasks.Count > 0 || ImmediateTasks.Count > 0;
    public  int                  MaxTasks { get; private set; }
    public  bool                 AbortOnTimeout { get; set; } = false;
    public  long                 AbortAt { get; private set; }
    public  bool                 ShowDebug { get; set; }
    public  int                  TimeLimitMS { get; set; } = 10000;
    public  bool                 TimeoutSilently { get; set; } = false;
    private Action<string>       LogTimeout => TimeoutSilently ? NotifyHelper.Verbose : NotifyHelper.Error;

    private void Tick(object? _)
    {
        if (CurrentTask == null)
        {
            if (ImmediateTasks.TryDequeue(out var immediateTask))
            {
                CurrentTask = immediateTask;
                if (ShowDebug)
                {
                    NotifyHelper.Debug(
                        $"Starting to execute immediate task: {CurrentTask.Name ?? CurrentTask.Action.GetMethodInfo().Name}");
                }

                AbortAt = Environment.TickCount64 + CurrentTask.TimeLimitMS;
            }
            else if (Tasks.TryDequeue(out var task))
            {
                CurrentTask = task;
                if (ShowDebug)
                {
                    NotifyHelper.Debug(
                        $"Starting to execute task: {CurrentTask.Name ?? CurrentTask.Action.GetMethodInfo().Name}");
                }

                AbortAt = Environment.TickCount64 + CurrentTask.TimeLimitMS;
            }
            else
                MaxTasks = 0;
        }
        else
        {
            try
            {
                var result = CurrentTask.Action();
                switch (result)
                {
                    case true:
                    {
                        if (ShowDebug)
                        {
                            NotifyHelper.Debug(
                                $"Task {CurrentTask.Name ?? CurrentTask.Action.GetMethodInfo().Name} completed successfully");
                        }

                        CurrentTask = null;
                        break;
                    }
                    case false:
                    {
                        if (Environment.TickCount64 > AbortAt)
                        {
                            if (CurrentTask.AbortOnTimeout)
                            {
                                LogTimeout($"Clearing {Tasks.Count} remaining tasks because of timeout");
                                Tasks.Clear();
                                ImmediateTasks.Clear();
                            }

                            throw new TimeoutException(
                                $"Task {CurrentTask.Name ?? CurrentTask.Action.GetMethodInfo().Name} took too long to execute");
                        }

                        break;
                    }
                    default:
                        NotifyHelper.Warning(
                            $"Clearing {Tasks.Count} remaining tasks because there was a signal from task {CurrentTask.Name ?? CurrentTask.Action.GetMethodInfo().Name} to abort");

                        Abort();
                        break;
                }
            }
            catch (TimeoutException e)
            {
                LogTimeout($"{e.Message}\n{e.StackTrace}");
                CurrentTask = null;
            }
            catch (Exception e)
            {
                NotifyHelper.Error("Errors in dequeueing tasks", e);
                CurrentTask = null;
            }
        }
    }

    public void SetStepMode(bool enabled)
    {
        Service.Framework.Update -= Tick;
        if (!enabled)
            Service.Framework.Update += Tick;
    }

    public void Step() => Tick(null);

    public void Abort()
    {
        Tasks.Clear();
        ImmediateTasks.Clear();
        CurrentTask = null;
    }

    public void Dispose()
    {
        Service.Framework.Update -= Tick;
        Instances.Remove(this);
    }

    public static void DisposeAll()
    {
        var i = 0;
        foreach (var manager in Instances)
        {
            Service.Framework.Update -= manager.Tick;
            i++;
        }

        if (i > 0)
            NotifyHelper.Debug($"Auto-disposing {i} task managers");

        Instances.Clear();
    }
}
