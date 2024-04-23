using System.Linq;
using DailyRoutines.Managers;

namespace DailyRoutines.Helpers;

public static class Utils
{
    public static bool IsChineseString(string text)
    {
        return text.All(IsChineseCharacter);
    }

    public static bool IsChineseCharacter(char c)
    {
        return (c >= 0x4E00 && c <= 0x9FA5) || (c >= 0x3400 && c <= 0x4DB5);
    }
}
