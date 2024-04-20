using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Dalamud.Game.Text.SeStringHandling;

namespace DailyRoutines.Managers;

public partial class LanguageManager
{
    public static string LangsDirectory { get; private set; } = null!;
    public string Language { get; private set; }

    private readonly Dictionary<string, string>? resourceData;

    public static readonly TranslationInfo[] LanguageNames =
    [
        new TranslationInfo { Language = "ChineseSimplified", DisplayName = "简体中文", Translators = ["AtmoOmen"] }
    ];

    public LanguageManager(string languageName, bool isDev = false, string devLangPath = "")
    {
        LangsDirectory = Path.Join(Path.GetDirectoryName(Service.PluginInterface.AssemblyLocation.FullName),
                                   "Managers", "Langs");

        if (isDev)
            resourceData = LoadResourceFile(devLangPath);
        else
        {
            if (LanguageNames.All(x => x.Language != languageName)) languageName = "ChineseSimplified";

            var resourcePath = Path.Join(LangsDirectory, languageName + ".resx");
            if (!File.Exists(resourcePath)) LanguageUpdater.DownloadLanguageFilesAsync().GetAwaiter().GetResult();
            resourceData = LoadResourceFile(resourcePath);
        }

        Language = languageName;
    }

    private static Dictionary<string, string> LoadResourceFile(string filePath)
    {
        var data = new Dictionary<string, string>();

        if (!File.Exists(filePath))
        {
            LanguageUpdater.DownloadLanguageFilesAsync().GetAwaiter().GetResult();
            if (!File.Exists(filePath)) return data;
        }

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

    [GeneratedRegex("\\{(\\d+)\\}")]
    private static partial Regex SeStringRegex();
}

public class TranslationInfo
{
    public string Language { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string[] Translators { get; set; } = null!;
}
