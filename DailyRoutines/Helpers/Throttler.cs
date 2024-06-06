using System;
using System.Collections.Generic;

namespace DailyRoutines.Helpers;

public class Throttler<T> where T : notnull
{
    public IReadOnlyCollection<T> ThrottleNames => throttlers.Keys;

    private readonly Dictionary<T, long> throttlers = [];

    public bool Throttle(T name, int milliseconds = 500, bool rethrottle = false)
    {
        if (throttlers.TryGetValue(name, out var lastThrottleTime) &&
            !rethrottle &&
            Environment.TickCount64 <= lastThrottleTime)
            return false;

        throttlers[name] = Environment.TickCount64 + milliseconds;
        return true;
    }

    public bool Check(T name) => !throttlers.TryGetValue(name, out var lastThrottleTime) || 
                                 Environment.TickCount64 > lastThrottleTime;

    public long GetRemainingTime(T name, bool allowNegative = false)
    {
        if (!throttlers.TryGetValue(name, out var lastThrottleTime))
            return allowNegative ? -Environment.TickCount64 : 0;

        var remainingTime = lastThrottleTime - Environment.TickCount64;
        return allowNegative ? remainingTime : Math.Max(remainingTime, 0);
    }
}
