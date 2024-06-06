using System;

namespace CurrencyTrackerRE.Managers;

public partial class TaskManager
{
    public void Enqueue(Func<bool?> task, string? name = null)
    {
        Tasks.Add(new(task, TimeLimitMS, AbortOnTimeout, name));
        MaxTasks++;
    }

    public void Enqueue(Func<bool?> task, int timeLimitMs, string? name = null)
    {
        Tasks.Add(new(task, timeLimitMs, AbortOnTimeout, name));
        MaxTasks++;
    }

    public void Enqueue(Func<bool?> task, bool abortOnTimeout, string? name = null)
    {
        Tasks.Add(new(task, TimeLimitMS, abortOnTimeout, name));
        MaxTasks++;
    }

    public void Enqueue(Func<bool?> task, int timeLimitMs, bool abortOnTimeout, string? name = null)
    {
        Tasks.Add(new(task, timeLimitMs, abortOnTimeout, name));
        MaxTasks++;
    }

    public void Enqueue(Action task, string? name = null)
    {
        Tasks.Add(new(() =>
        {
            task();
            return true;
        }, TimeLimitMS, AbortOnTimeout, name));

        MaxTasks++;
    }

    public void Enqueue(Action task, int timeLimitMs, string? name = null)
    {
        Tasks.Add(new(() =>
        {
            task();
            return true;
        }, timeLimitMs, AbortOnTimeout, name));

        MaxTasks++;
    }

    public void Enqueue(Action task, bool abortOnTimeout, string? name = null)
    {
        Tasks.Add(new(() =>
        {
            task();
            return true;
        }, TimeLimitMS, abortOnTimeout, name));

        MaxTasks++;
    }

    public void Enqueue(Action task, int timeLimitMs, bool abortOnTimeout, string? name = null)
    {
        Tasks.Add(new(() =>
        {
            task();
            return true;
        }, timeLimitMs, abortOnTimeout, name));

        MaxTasks++;
    }

    public void DelayNext(int delayMS, bool useFrameThrottler = false) =>
        DelayNext("DelayNextEnqueue", delayMS, useFrameThrottler);

    public void DelayNext(string uniqueName, int delayMS, bool useFrameThrottler = false)
    {
        if (useFrameThrottler)
        {
            Enqueue(() => FrameThrottler.Throttle(uniqueName, delayMS),
                    $"FrameThrottler.Throttle({uniqueName}, {delayMS})");

            Enqueue(() => FrameThrottler.Check(uniqueName), $"FrameThrottler.Check({uniqueName})");
        }
        else
        {
            Enqueue(() => Throttler.Throttle(uniqueName, delayMS), $"Throttler.Throttle({uniqueName}, {delayMS})");
            Enqueue(() => Throttler.Check(uniqueName), $"Throttler.Check({uniqueName})");
        }

        MaxTasks += 2;
    }
}
