using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Dalamud.Game.Text.SeStringHandling;

namespace DailyRoutines.Managers;

public partial class LanguageManager : IDailyManager
{
    private static Dictionary<string, string>? _resourceData;
    private static readonly Dictionary<string, SeString> _seStringCache = new();

    private void Init()
    {
        var filePath = Path.Combine(
            Path.GetDirectoryName(Service.PluginInterface.AssemblyLocation.FullName)!,
            "Managers", "Langs", "ChineseSimplified.resx");

        _resourceData = LoadResourceFile(filePath);
    }

    private static Dictionary<string, string> LoadResourceFile(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var doc = XDocument.Load(stream);
        return doc.Root!
                  .Elements("data")
                  .ToDictionary(
                      e => e.Attribute("name")!.Value,
                      e => e.Element("value")!.Value,
                      StringComparer.Ordinal
                  );
    }

    public string GetText(string key, params object[] args)
    {
        if (_resourceData!.TryGetValue(key, out var format))
            return args.Length == 0 ? format : string.Format(format, args);

        return LogErrorAndReturnKey(key);
    }

    public SeString GetSeString(string key, params object[] args)
    {
        if (args.Length == 0 && _seStringCache.TryGetValue(key, out var cachedSeString))
            return cachedSeString;

        if (!_resourceData!.TryGetValue(key, out var format))
        {
            LogErrorAndReturnKey(key);
            return new SeString();
        }

        var ssb = new SeStringBuilder();
        var lastIndex = 0;
        var matches = SeStringRegex().Matches(format);

        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            ssb.AddUiForeground(format.Substring(lastIndex, match.Index - lastIndex), 2);
            lastIndex = match.Index + match.Length;

            if (int.TryParse(match.Groups[1].Value, out var argIndex) && argIndex >= 0 && argIndex < args.Length)
                AppendArgument(ssb, args[argIndex]);
        }

        ssb.AddUiForeground(format.Substring(lastIndex), 2);
        var result = ssb.Build();

        if (args.Length == 0)
            _seStringCache[key] = result;

        return result;
    }

    private static void AppendArgument(SeStringBuilder ssb, object arg)
    {
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

    private static string LogErrorAndReturnKey(string key)
    {
        Service.Log.Error($"Localization String {key} Not Found in Current Language!");
        return key;
    }

    private void Uninit()
    {
        _resourceData?.Clear();
        _seStringCache.Clear();
    }

    [GeneratedRegex("\\{(\\d+)\\}", RegexOptions.Compiled)]
    private static partial Regex SeStringRegex();
}
