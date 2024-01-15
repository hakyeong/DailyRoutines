namespace DailyRoutines.Managers;

public class TaskManager
{
    public static async Task<bool> WaitForExpectedResult(Func<bool> condition, bool expectedResult, TimeSpan timeout)
    {
        var startTime = DateTime.Now;
        while (DateTime.Now - startTime < timeout)
        {
            var conditionResult = await Service.Framework.RunOnFrameworkThread(condition);

            if (conditionResult == expectedResult)
                return true;

            await Task.Delay(100);
        }

        return false;
    }
}
