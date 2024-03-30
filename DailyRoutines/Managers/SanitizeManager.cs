using Dalamud.Game.Text;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DailyRoutines.Managers;

public class SanitizeManager
{
    private static readonly Lazy<Dictionary<char, string>> ChineseSimplifiedInitializer = new(() =>
    {
        return Enum.GetValues(typeof(SeIconChar))
                   .Cast<SeIconChar>()
                   .ToDictionary(icon => char.ConvertFromUtf32((int)icon)[0], icon => string.Empty);
    });

    public static string Sanitize(string str)
    {
        var chineseSimplified = ChineseSimplifiedInitializer.Value;
        return SanitizeByDict(str, chineseSimplified);
    }

    private static string SanitizeByDict(string str, Dictionary<char, string> dict)
    {
        return dict.Aggregate(str, (current, kvp) => current.Replace(kvp.Key.ToString(), kvp.Value));
    }
}
