namespace DailyRoutines.Helpers;

public class ThrottlerHelper
{
    public static Throttler<string>      Throttler      { get; } = new();
    public static FrameThrottler<string> FrameThrottler { get; } = new();
}
