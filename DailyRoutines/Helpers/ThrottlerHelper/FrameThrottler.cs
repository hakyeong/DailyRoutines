using System.Collections.Generic;
using DailyRoutines.Managers;

namespace DailyRoutines.Helpers;

public class FrameThrottler<T> where T : notnull
{
    private Dictionary<T, long> throttlers = [];
    private long SFrameCount => (long)Service.PluginInterface.UiBuilder.FrameCount;

    public IReadOnlyCollection<T> ThrottleNames => throttlers.Keys;

    public bool Throttle(T name, int frames = 60, bool rethrottle = false)
    {
        if (!throttlers.TryGetValue(name, out var frameCount))
        {
            frameCount = SFrameCount + frames;
            throttlers[name] = frameCount;
            return true;
        }

        if (SFrameCount > frameCount)
        {
            throttlers[name] = SFrameCount + frames;
            return true;
        }

        if (rethrottle) throttlers[name] = SFrameCount + frames;
        return false;
    }

    public bool Check(T name)
    {
        if (!throttlers.TryGetValue(name, out var frameCount)) return true;
        return SFrameCount > frameCount;
    }

    public long GetRemainingTime(T name, bool allowNegative = false)
    {
        if (!throttlers.TryGetValue(name, out var throttler)) return allowNegative ? -SFrameCount : 0;
        var ret = throttler - SFrameCount;
        if (allowNegative)
            return ret;

        return ret > 0 ? ret : 0;
    }
}
