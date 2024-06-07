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

    private TaskHelperTask?            CurrentTask { get; set; }
    public  string                     CurrentTaskName => CurrentTask?.Name;
    private SortedSet<TaskHelperQueue> Queues { get; set; } = [new(1), new(0)];
    private List<TaskHelperTask>       Tasks { get; set; } = [];
    private List<TaskHelperTask>       ImmediateTasks { get; set; } = [];
    public  List<string>               TaskStack => Queues.SelectMany(q => q.Tasks.Select(t => t.Name)).ToList();
    public  int                        NumQueuedTasks => Queues.Sum(q => q.Tasks.Count) + (CurrentTask == null ? 0 : 1);
    public  bool                       IsBusy => CurrentTask != null || Queues.Any(q => q.Tasks.Count > 0);
    public  int                        MaxTasks { get; private set; }
    public  bool                       AbortOnTimeout { get; set; } = false;
    public  long                       AbortAt { get; private set; }
    public  bool                       ShowDebug { get; set; }
    public  int                        TimeLimitMS { get; set; } = 10000;
    public  bool                       TimeoutSilently { get; set; } = false;
    private Action<string>             LogTimeout => TimeoutSilently ? NotifyHelper.Verbose : NotifyHelper.Warning;

    private void Tick(object? _)
    {
        if (CurrentTask == null)
        {
            foreach (var queue in Queues)
            {
                if (queue.Tasks.TryDequeue(out var task))
                {
                    CurrentTask = task;
                    if (ShowDebug)
                        NotifyHelper.Debug($"开始执行任务: {CurrentTask.Name ?? CurrentTask.Action.GetMethodInfo().Name}");

                    AbortAt = Environment.TickCount64 + CurrentTask.TimeLimitMS;
                    break;
                }
            }

            if (CurrentTask == null) MaxTasks = 0;
        }
        else
        {
            try
            {
                var result = CurrentTask.Action();
                switch (result)
                {
                    case true:
                        if (ShowDebug)
                            NotifyHelper.Debug($"已完成任务: {CurrentTask.Name ?? CurrentTask.Action.GetMethodInfo().Name}");

                        CurrentTask = null;
                        break;

                    case false:
                        if (Environment.TickCount64 > AbortAt)
                        {
                            if (CurrentTask.AbortOnTimeout)
                            {
                                LogTimeout("已清理所有剩余任务 (原因: 等待超时)");
                                foreach (var queue in Queues)
                                    queue.Tasks.Clear();
                            }

                            throw new TimeoutException(
                                $"任务 {CurrentTask.Name ?? CurrentTask.Action.GetMethodInfo().Name} 执行时间过长");
                        }
                        break;

                    default:
                        NotifyHelper.Warning(
                            $"正在清理所有剩余任务 (原因: 任务 {CurrentTask.Name ?? CurrentTask.Action.GetMethodInfo().Name} 要求终止)");

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
                NotifyHelper.Error("执行任务过程中出现错误", e);
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
        foreach (var queue in Queues)
            queue.Tasks.Clear();

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
        foreach (var instance in Instances)
        {
            Service.Framework.Update -= instance.Tick;
            i++;
        }

        if (i > 0)
            NotifyHelper.Debug($"已自动清理了 {i} 个队列管理器");

        Instances.Clear();
    }
}
