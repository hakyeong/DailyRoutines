using System;

namespace DailyRoutines.Helpers;

public partial class TaskHelper
{
    public void EnqueueImmediate(Func<bool?> task, string? name = null)
    {
        ImmediateTasks.Add(new(task, TimeLimitMS, AbortOnTimeout, name));
        MaxTasks++;
    }

    public void EnqueueImmediate(Func<bool?> task, int timeLimitMs, string? name = null)
    {
        ImmediateTasks.Add(new(task, timeLimitMs, AbortOnTimeout, name));
        MaxTasks++;
    }

    public void EnqueueImmediate(Func<bool?> task, bool abortOnTimeout, string? name = null)
    {
        ImmediateTasks.Add(new(task, TimeLimitMS, abortOnTimeout, name));
        MaxTasks++;
    }

    public void EnqueueImmediate(Func<bool?> task, int timeLimitMs, bool abortOnTimeout, string? name = null)
    {
        ImmediateTasks.Add(new(task, timeLimitMs, abortOnTimeout, name));
        MaxTasks++;
    }

    public void EnqueueImmediate(Action task, string? name = null)
    {
        ImmediateTasks.Add(new(() =>
        {
            task();
            return true;
        }, TimeLimitMS, AbortOnTimeout, name));

        MaxTasks++;
    }

    public void EnqueueImmediate(Action task, int timeLimitMs, string? name = null)
    {
        ImmediateTasks.Add(new(() =>
        {
            task();
            return true;
        }, timeLimitMs, AbortOnTimeout, name));

        MaxTasks++;
    }

    public void EnqueueImmediate(Action task, bool abortOnTimeout, string? name = null)
    {
        ImmediateTasks.Add(new(() =>
        {
            task();
            return true;
        }, TimeLimitMS, abortOnTimeout, name));

        MaxTasks++;
    }

    public void EnqueueImmediate(Action task, int timeLimitMs, bool abortOnTimeout, string? name = null)
    {
        ImmediateTasks.Add(new(() =>
        {
            task();
            return true;
        }, timeLimitMs, abortOnTimeout, name));

        MaxTasks++;
    }

    public void DelayNextImmediate(int delayMS, bool useFrameThrottler = false) =>
        DelayNextImmediate("DelayNexyImmediate", delayMS, useFrameThrottler);

    public void DelayNextImmediate(string uniqueName, int delayMS, bool useFrameThrottler = false)
    {
        if (useFrameThrottler)
        {
            EnqueueImmediate(() => FrameThrottler.Throttle(uniqueName, delayMS),
                             $"FrameThrottler.Throttle({uniqueName}, {delayMS})");

            EnqueueImmediate(() => FrameThrottler.Check(uniqueName), $"FrameThrottler.Check({uniqueName})");
        }
        else
        {
            EnqueueImmediate(() => Throttler.Throttle(uniqueName, delayMS),
                             $"Throttler.Throttle({uniqueName}, {delayMS})");

            EnqueueImmediate(() => Throttler.Check(uniqueName), $"Throttler.Check({uniqueName})");
        }

        MaxTasks += 2;
    }
}
