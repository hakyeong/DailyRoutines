using System;
using System.Linq;

namespace DailyRoutines.Helpers;

public partial class TaskHelper
{
    public void Insert(Func<bool?> task, string? name = null, uint weight = 0)
    {
        InsertQueueTask(new TaskHelperTask(task, TimeLimitMS, AbortOnTimeout, name), weight);
    }

    public void Insert(Func<bool?> task, int timeLimitMs, string? name = null, uint weight = 0)
    {
        InsertQueueTask(new TaskHelperTask(task, timeLimitMs, AbortOnTimeout, name), weight);
    }

    public void Insert(Func<bool?> task, bool abortOnTimeout, string? name = null, uint weight = 0)
    {
        InsertQueueTask(new TaskHelperTask(task, TimeLimitMS, abortOnTimeout, name), weight);
    }

    public void Insert(Func<bool?> task, int timeLimitMs, bool abortOnTimeout, string? name = null, uint weight = 0)
    {
        InsertQueueTask(new TaskHelperTask(task, timeLimitMs, abortOnTimeout, name), weight);
    }

    public void Insert(Action task, string? name = null, uint weight = 0)
    {
        InsertQueueTask(new TaskHelperTask(() => { task(); return true; }, TimeLimitMS, AbortOnTimeout, name), weight);
    }

    private void InsertQueueTask(TaskHelperTask task, uint weight)
    {
        var queue = Queues.FirstOrDefault(q => q.Weight == weight) ?? AddQueueAndGet(weight);
        queue.Tasks.Insert(0, task);
        MaxTasks++;
    }

    private TaskHelperQueue AddQueueAndGet(uint weight)
    {
        var newQueue = new TaskHelperQueue(weight);
        Queues.Add(newQueue);
        return newQueue;
    }

    public void InsertDelayNext(int delayMS, bool useFrameThrottler = false, uint weight = 0) =>
        InsertDelayNext("DelayNextInsert", delayMS, useFrameThrottler, weight);

    public void InsertDelayNext(string uniqueName, int delayMS, bool useFrameThrottler = false, uint weight = 0)
    {
        if (useFrameThrottler)
        {
            Insert(() => FrameThrottler.Check(uniqueName), $"FrameThrottler.Check({uniqueName})", weight);
            Insert(() => FrameThrottler.Throttle(uniqueName, delayMS), $"FrameThrottler.Throttle({uniqueName}, {delayMS})", weight);
        }
        else
        {
            Insert(() => Throttler.Check(uniqueName), $"Throttler.Check({uniqueName})", weight);
            Insert(() => Throttler.Throttle(uniqueName, delayMS), $"Throttler.Throttle({uniqueName}, {delayMS})", weight);
        }

        MaxTasks += 2;
    }

}
