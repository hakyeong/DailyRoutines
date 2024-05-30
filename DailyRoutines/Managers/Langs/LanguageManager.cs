using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Dalamud.Game.Text.SeStringHandling;

namespace DailyRoutines.Managers;

public partial class LanguageManager : IDailyManager
{
    private static Dictionary<string, string>? resourceData;

    private void Init()
    {
        resourceData = LoadResourceFile(Path.Join(Path.GetDirectoryName(Service.PluginInterface.AssemblyLocation.FullName), 
                                                  "Managers", "Langs" , "ChineseSimplified.resx"));
    }

    private static Dictionary<string, string> LoadResourceFile(string filePath)
    {
        var data = new Dictionary<string, string>();

        var doc = XDocument.Load(filePath);
        var dataElements = doc.Root.Elements("data");
        foreach (var element in dataElements)
        {
            var name = element.Attribute("name")?.Value;
            var value = element.Element("value")?.Value;
            if (!string.IsNullOrEmpty(name) && value != null) data[name] = value;
        }

        return data;
    }

    public string GetText(string key, params object[] args)
    {
        resourceData.TryGetValue(key, out var format);

        if (string.IsNullOrEmpty(format))
        {
            Service.Log.Error($"Localization String {key} Not Found in Current Language!");
            return key;
        }

        return string.Format(format, args);
    }

    public SeString GetSeString(string key, params object[] args)
    {
        resourceData.TryGetValue(key, out var format);
        var ssb = new SeStringBuilder();
        var lastIndex = 0;

        foreach (var match in SeStringRegex().Matches(format).Cast<Match>())
        {
            ssb.AddUiForeground(format[lastIndex..match.Index], 2);
            lastIndex = match.Index + match.Length;

            if (int.TryParse(match.Groups[1].Value, out var argIndex) && argIndex >= 0 && argIndex < args.Length)
            {
                var arg = args[argIndex];
                switch (arg)
                {
                    case SeString seString:
                        ssb.Append(seString);
                        break;
                    case BitmapFontIcon icon:
                        ssb.AddIcon(icon);
                        break;
                    default:
                        ssb.AddUiForeground(arg.ToString(), 2);
                        break;
                }
            }
        }

        ssb.AddUiForeground(format[lastIndex..], 2);
        return ssb.Build();
    }

    private void Uninit() { }

    [GeneratedRegex("\\{(\\d+)\\}")]
    private static partial Regex SeStringRegex();
}
