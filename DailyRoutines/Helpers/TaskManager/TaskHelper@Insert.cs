using System;

namespace DailyRoutines.Helpers;

public partial class TaskHelper
{
    public void Insert(Func<bool?> task, string? name = null)
    {
        Tasks.Insert(0, new(task, TimeLimitMS, AbortOnTimeout, name));
        MaxTasks++;
    }

    public void Insert(Func<bool?> task, int timeLimitMs, string? name = null)
    {
        Tasks.Insert(0, new(task, timeLimitMs, AbortOnTimeout, name));
        MaxTasks++;
    }

    public void Insert(Func<bool?> task, bool abortOnTimeout, string? name = null)
    {
        Tasks.Insert(0, new(task, TimeLimitMS, abortOnTimeout, name));
        MaxTasks++;
    }

    public void Insert(Func<bool?> task, int timeLimitMs, bool abortOnTimeout, string? name = null)
    {
        Tasks.Insert(0, new(task, timeLimitMs, abortOnTimeout, name));
        MaxTasks++;
    }

    public void Insert(Action task, string? name = null)
    {
        Tasks.Insert(0, new(() =>
        {
            task();
            return true;
        }, TimeLimitMS, AbortOnTimeout, name));

        MaxTasks++;
    }

    public void Insert(Action task, int timeLimitMs, string? name = null)
    {
        Tasks.Insert(0, new(() =>
        {
            task();
            return true;
        }, timeLimitMs, AbortOnTimeout, name));

        MaxTasks++;
    }

    public void Insert(Action task, bool abortOnTimeout, string? name = null)
    {
        Tasks.Insert(0, new(() =>
        {
            task();
            return true;
        }, TimeLimitMS, abortOnTimeout, name));

        MaxTasks++;
    }

    public void Insert(Action task, int timeLimitMs, bool abortOnTimeout, string? name = null)
    {
        Tasks.Insert(0, new(() =>
        {
            task();
            return true;
        }, timeLimitMs, abortOnTimeout, name));

        MaxTasks++;
    }

    public void InsertDelayNext(int delayMS, bool useFrameThrottler = false) =>
        InsertDelayNext("DelayNextTask", delayMS, useFrameThrottler);

    public void InsertDelayNext(string uniqueName, int delayMS, bool useFrameThrottler = false)
    {
        if (useFrameThrottler)
        {
            Insert(() => FrameThrottler.Check(uniqueName), $"FrameThrottler.Check({uniqueName})");
            Insert(() => FrameThrottler.Throttle(uniqueName, delayMS), $"FrameThrottler.Throttle({uniqueName}, {delayMS})");
        }
        else
        {
            Insert(() => Throttler.Check(uniqueName), $"Throttler.Check({uniqueName})");
            Insert(() => Throttler.Throttle(uniqueName, delayMS), $"Throttler.Throttle({uniqueName}, {delayMS})");
        }

        MaxTasks += 2;
    }
}
