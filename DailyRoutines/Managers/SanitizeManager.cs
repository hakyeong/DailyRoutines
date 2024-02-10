using System;
using System.Collections.Generic;
using Dalamud.Game.Text;

namespace DailyRoutines.Managers;

public class SanitizeManager
{
    private static Dictionary<char, string>? ChineseSimplified;

    public static string Sanitize(string str)
    {
        if (ChineseSimplified == null)
        {
            ChineseSimplified = [];
            foreach (SeIconChar icon in Enum.GetValues(typeof(SeIconChar)))
            {
                ChineseSimplified.Add(char.ConvertFromUtf32((int)icon)[0], string.Empty);
            }
        }
        
        return SanitizeByDict(str, ChineseSimplified);
    }

    private static string SanitizeByDict(string str, Dictionary<char, string> dict)
    {
        foreach (var kvp in dict)
        {
            str = str.Replace(kvp.Key.ToString(), kvp.Value);
        }
        return str;
    }

}
