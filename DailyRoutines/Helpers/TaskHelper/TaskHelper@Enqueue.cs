using System;
using System.Linq;

namespace DailyRoutines.Helpers;

public partial class TaskHelper
{
    public void Enqueue(Func<bool?> task, string? name = null, uint weight = 0)
    {
        EnsureQueueExists(weight);
        var queue = Queues.First(q => q.Weight == weight);
        queue.Tasks.Add(new TaskHelperTask(task, TimeLimitMS, AbortOnTimeout, name));
        MaxTasks++;
    }

    public void Enqueue(Func<bool?> task, int timeLimitMs, string? name = null, uint weight = 0)
    {
        EnsureQueueExists(weight);
        var queue = Queues.First(q => q.Weight == weight);
        queue.Tasks.Add(new TaskHelperTask(task, timeLimitMs, AbortOnTimeout, name));
        MaxTasks++;
    }

    public void Enqueue(Func<bool?> task, bool abortOnTimeout, string? name = null, uint weight = 0)
    {
        EnsureQueueExists(weight);
        var queue = Queues.First(q => q.Weight == weight);
        queue.Tasks.Add(new TaskHelperTask(task, TimeLimitMS, abortOnTimeout, name));
        MaxTasks++;
    }

    public void Enqueue(Func<bool?> task, int timeLimitMs, bool abortOnTimeout, string? name = null, uint weight = 0)
    {
        EnsureQueueExists(weight);
        var queue = Queues.First(q => q.Weight == weight);
        queue.Tasks.Add(new TaskHelperTask(task, timeLimitMs, abortOnTimeout, name));
        MaxTasks++;
    }

    public void Enqueue(Action task, string? name = null, uint weight = 0)
    {
        Enqueue(() => { task(); return true; }, TimeLimitMS, AbortOnTimeout, name, weight);
    }

    public void Enqueue(Action task, int timeLimitMs, string? name = null, uint weight = 0)
    {
        Enqueue(() => { task(); return true; }, timeLimitMs, AbortOnTimeout, name, weight);
    }

    public void Enqueue(Action task, bool abortOnTimeout, string? name = null, uint weight = 0)
    {
        Enqueue(() => { task(); return true; }, TimeLimitMS, abortOnTimeout, name, weight);
    }

    public void Enqueue(Action task, int timeLimitMs, bool abortOnTimeout, string? name = null, uint weight = 0)
    {
        Enqueue(() => { task(); return true; }, timeLimitMs, abortOnTimeout, name, weight);
    }

    private void EnsureQueueExists(uint weight)
    {
        var queue = Queues.FirstOrDefault(q => q.Weight == weight);
        if (queue == null)
        {
            queue = new TaskHelperQueue(weight);
            Queues.Add(queue);
        }
    }

    public void DelayNext(int delayMS, bool useFrameThrottler = false, uint weight = 0) =>
        DelayNext("DelayNextEnqueue", delayMS, useFrameThrottler, weight);

    public void DelayNext(string uniqueName, int delayMS, bool useFrameThrottler = false, uint weight = 0)
    {
        if (useFrameThrottler)
        {
            Enqueue(() => FrameThrottler.Throttle(uniqueName, delayMS),
                    $"FrameThrottler.Throttle({uniqueName}, {delayMS})", weight);

            Enqueue(() => FrameThrottler.Check(uniqueName), $"FrameThrottler.Check({uniqueName})", weight);
        }
        else
        {
            Enqueue(() => Throttler.Throttle(uniqueName, delayMS), $"Throttler.Throttle({uniqueName}, {delayMS})", weight);
            Enqueue(() => Throttler.Check(uniqueName), $"Throttler.Check({uniqueName})", weight);
        }

        MaxTasks += 2;
    }
}
