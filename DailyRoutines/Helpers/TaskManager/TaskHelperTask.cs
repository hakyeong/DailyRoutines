using System;

namespace DailyRoutines.Helpers;

public record TaskManagerTask(Func<bool?> Action, int TimeLimitMS, bool AbortOnTimeout, string Name)
{
    public bool AbortOnTimeout = AbortOnTimeout;
    public Func<bool?> Action = Action;
    public string Name = Name;
    public int TimeLimitMS = TimeLimitMS;
}
