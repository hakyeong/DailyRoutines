using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Reflection;
using System.Threading.Tasks;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using DailyRoutines.Modules;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using ImGuiNET;
using Newtonsoft.Json;

namespace DailyRoutines.Windows;

public class Main : Window, IDisposable
{
    private static readonly ConcurrentDictionary<Type, (string Name, string Title, string Description)>
        ModuleCache = [];

    private static readonly Dictionary<ModuleCategories, List<Type>> ModuleCategories = [];
    private static Type[]? AllModules;

    internal static string SearchString = string.Empty;
    private static string ConflictKeySearchString = string.Empty;

    public class Release
    {
        public int id { get; set; }
        public string tag_name { get; set; } = null!;
        public string name { get; set; } = null!;
        public string body { get; set; } = null!;
        public DateTime published_at { get; set; }
        public List<Asset> assets { get; set; } = null!;
    }

    public class Asset
    {
        public string name { get; set; } = null!;
        public int download_count { get; set; }
    }

    private static readonly HttpClient client = new();
    private static int TotalDownloadCounts;
    private static int LatestDownloadCounts;
    private static string? CurrentVersion;
    private static string? LatestVersion;
    private static string? LatestChangelog;
    private static string? LatestPublishTime;

    public Main(Plugin plugin) : base("Daily Routines - Main")
    {
        Flags = ImGuiWindowFlags.NoScrollbar;

        Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => typeof(DailyModuleBase).IsAssignableFrom(t) && t is { IsClass: true, IsAbstract: false })
                .OrderBy(t => Service.Lang.GetText(t.GetCustomAttribute<ModuleDescriptionAttribute>()?.TitleKey))
                .ToList()
                .ForEach(type =>
                {
                    var category = type.GetCustomAttribute<ModuleDescriptionAttribute>()?.Category;
                    if (category.HasValue)
                    {
                        if (!ModuleCategories.ContainsKey(category.Value))
                            ModuleCategories[category.Value] = [];
                        ModuleCategories[category.Value].Add(type);
                    }
                });
        AllModules = ModuleCategories.Values.SelectMany(list => list).ToArray();

        Task.Run(async () =>
        {
            TotalDownloadCounts = await GetTotalDownloadsAsync("AtmoOmen", "DailyRoutines");
            var latestVersion = await GetLatestVersionAsync("AtmoOmen", "DailyRoutines");

            LatestVersion = latestVersion.version;
            LatestDownloadCounts = latestVersion.downloadCount;
            LatestChangelog = latestVersion.description;
            LatestPublishTime = latestVersion.publishTime;
        });

        GetCurrentVersion();
    }

    public override void Draw()
    {
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##MainWindow-SearchInput", $"{Service.Lang.GetText("PleaseSearch")}...",
                                ref SearchString, 100);
        ImGui.Separator();

        if (ImGui.BeginTabBar("BasicTab", ImGuiTabBarFlags.Reorderable | ImGuiTabBarFlags.FittingPolicyScroll))
        {
            if (string.IsNullOrEmpty(SearchString))
            {
                foreach (var module in ModuleCategories) DrawTabItemModules(module.Value, module.Key);
                DrawTabSettings();
            }
            else
                DrawTabItemModulesSearchResult(AllModules);

            ImGui.EndTabBar();
        }
    }

    private static void DrawTabItemModules(IReadOnlyList<Type> modules, ModuleCategories category)
    {
        if (ImGui.BeginTabItem(Service.Lang.GetText(category.ToString())))
        {
            for (var i = 0; i < modules.Count; i++)
            {
                ImGui.PushID($"{modules[i]}");
                DrawModuleCheckbox(modules[i], modules.Count, i);
                ImGui.PopID();
            }

            ImGui.EndTabItem();
        }
    }

    private static void DrawTabItemModulesSearchResult(IReadOnlyList<Type> modules)
    {
        if (ImGui.BeginTabItem(Service.Lang.GetText("SearchResult")))
        {
            for (var i = 0; i < modules.Count; i++)
            {
                ImGui.PushID($"{modules[i]}");
                DrawModuleCheckbox(modules[i], modules.Count, i);
                ImGui.PopID();
            }

            ImGui.EndTabItem();
        }
    }

    private static void DrawModuleCheckbox(Type module, int modulesCount, int index)
    {
        var (boolName, title, description) = ModuleCache.GetOrAdd(module, m =>
        {
            var attributes = m.GetCustomAttributes(typeof(ModuleDescriptionAttribute), false);
            if (attributes.Length == 0) return (m.Name, string.Empty, string.Empty);

            var content = (ModuleDescriptionAttribute)attributes[0];
            return (m.Name, Service.Lang.GetText(content.TitleKey), Service.Lang.GetText(content.DescriptionKey));
        });

        if (!Service.Config.ModuleEnabled.TryGetValue(boolName, out var tempModuleBool) ||
            string.IsNullOrEmpty(title) || string.IsNullOrEmpty(description) ||
            (!string.IsNullOrWhiteSpace(SearchString) &&
             !title.Contains(SearchString, StringComparison.OrdinalIgnoreCase) &&
             !description.Contains(SearchString, StringComparison.OrdinalIgnoreCase)))
            return;

        var methodInfo = module.GetMethod("ConfigUI", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        var isWithUI = methodInfo != null && methodInfo.DeclaringType != typeof(ModuleBase);

        if (ImGuiOm.CheckboxColored($"##{module.Name}", ref tempModuleBool))
        {
            Service.Config.ModuleEnabled[boolName] = tempModuleBool;
            var component = ModuleManager.Modules[module];
            if (tempModuleBool) ModuleManager.Load(component);
            else ModuleManager.Unload(component);

            Service.Config.Save();
        }

        var moduleText = $"[{module.Name}]";
        ImGui.SameLine();
        var origCursorPos = ImGui.GetCursorPosX();
        ImGui.SetCursorPosX(ImGui.GetWindowWidth() - (ImGui.CalcTextSize(moduleText).X * 0.8f) -
                            (2 * ImGui.GetStyle().FramePadding.X));
        ImGui.SetWindowFontScale(0.8f);
        ImGui.TextDisabled(moduleText);
        ImGui.SetWindowFontScale(1f);

        ImGui.SameLine();
        ImGui.SetCursorPosX(origCursorPos);

        if (tempModuleBool)
        {
            if (isWithUI)
            {
                if (CollapsingHeader())
                {
                    ImGui.SetCursorPosX(origCursorPos);
                    ImGui.BeginGroup();
                    DrawModuleUI(module);
                    ImGui.EndGroup();
                }
            }
            else
                ImGui.TextColored(ImGuiColors.DalamudYellow, title);
        }
        else
            ImGui.Text(title);

        ImGui.SetCursorPosX(origCursorPos);
        ImGuiOm.TextDisabledWrapped(description);
        if (index < modulesCount - 1) ImGui.Separator();

        return;

        bool CollapsingHeader()
        {
            ImGui.PushStyleColor(ImGuiCol.Text, tempModuleBool ? ImGuiColors.DalamudYellow : ImGuiColors.DalamudWhite);
            ImGui.PushStyleColor(ImGuiCol.Header, ImGui.ColorConvertFloat4ToU32(new Vector4(0)));
            var collapsingHeader = ImGui.CollapsingHeader($"{title}##{module.Name}");
            ImGui.PopStyleColor(2);

            return collapsingHeader;
        }
    }

    private static void DrawModuleUI(Type module)
    {
        var moduleInstance = ModuleManager.Modules[module];

        moduleInstance.ConfigUI();
    }

    private static void DrawTabSettings()
    {
        if (ImGui.BeginTabItem(Service.Lang.GetText("Settings")))
        {
            #region Settings

            // 第一列
            ImGui.BeginGroup();
            ImGuiOm.TextIcon(FontAwesomeIcon.Globe, $"{Service.Lang.GetText("Language")}:");

            ImGui.SameLine();
            ImGui.SetNextItemWidth(180f * ImGuiHelpers.GlobalScale);
            if (ImGui.BeginCombo("##LanguagesList", Service.Config.SelectedLanguage))
            {
                for (var i = 0; i < LanguageManager.LanguageNames.Length; i++)
                {
                    var languageInfo = LanguageManager.LanguageNames[i];
                    if (ImGui.Selectable(languageInfo.DisplayName,
                                         Service.Config.SelectedLanguage == languageInfo.Language))
                        LanguageSwitchHandler(languageInfo.Language);

                    ImGuiOm.TooltipHover($"By: {string.Join(", ", languageInfo.Translators)}");

                    if (i + 1 != LanguageManager.LanguageNames.Length) ImGui.Separator();
                }

                ImGui.EndCombo();
            }

            ImGui.EndGroup();

            ImGui.SameLine();
            ImGui.Spacing();

            // 第二列
            ImGui.SameLine();
            ImGui.BeginGroup();
            ImGuiOm.TextIcon(FontAwesomeIcon.Keyboard, $"{Service.Lang.GetText("ConflictKey")}:");

            ImGui.SameLine();
            ImGui.SetNextItemWidth(100f * ImGuiHelpers.GlobalScale);
            if (ImGui.BeginCombo("##GlobalConflictHotkey", Service.Config.ConflictKey.ToString()))
            {
                ImGui.SetNextItemWidth(-1f);
                ImGui.InputTextWithHint("##ConflictKeySearchBar", $"{Service.Lang.GetText("PleaseSearch")}...",
                                        ref ConflictKeySearchString, 20);
                ImGui.Separator();

                foreach (VirtualKey keyToSelect in Enum.GetValues(typeof(VirtualKey)))
                {
                    if (!string.IsNullOrWhiteSpace(ConflictKeySearchString) && !keyToSelect.ToString()
                            .Contains(ConflictKeySearchString, StringComparison.OrdinalIgnoreCase)) continue;
                    if (ImGui.Selectable(keyToSelect.ToString())) Service.Config.ConflictKey = keyToSelect;
                }

                ImGui.EndCombo();
            }

            ImGuiOm.HelpMarker(Service.Lang.GetText("ConflictKeyHelp"));
            ImGui.EndGroup();

            #endregion

            ImGui.Separator();

            ImGuiHelpers.CenterCursorForText(Service.Lang.GetText("ContactHelp"));
            ImGui.TextColored(ImGuiColors.DalamudYellow, Service.Lang.GetText("ContactHelp"));

            ImGui.AlignTextToFramePadding();
            ImGui.TextColored(ImGuiColors.DalamudOrange, $"{Service.Lang.GetText("Contact")}:");

            ImGui.SameLine();
            if (ImGui.Button("GitHub")) Util.OpenLink("https://github.com/AtmoOmen/DailyRoutines");
            ImGui.SameLine();
            if (ImGui.Button("bilibili")) Util.OpenLink("https://www.bilibili.com/read/cv31823881/");

            ImGui.Separator();

            ImGui.TextColored(ImGuiColors.DalamudYellow, $"{Service.Lang.GetText("Info")}:");

            ImGui.SameLine();
            if (ImGui.SmallButton(Service.Lang.GetText("Refresh")))
            {
                Task.Run(async () =>
                {
                    TotalDownloadCounts = await GetTotalDownloadsAsync("AtmoOmen", "DailyRoutines");
                    var latestVersion = await GetLatestVersionAsync("AtmoOmen", "DailyRoutines");

                    LatestVersion = latestVersion.version;
                    LatestDownloadCounts = latestVersion.downloadCount;
                    LatestChangelog = latestVersion.description;
                    LatestPublishTime = latestVersion.publishTime;
                });
            }

            ImGui.TextColored(ImGuiColors.DalamudOrange, $"{Service.Lang.GetText("TotalDL")}:");

            ImGui.SameLine();
            ImGui.Text($"{TotalDownloadCounts}");

            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.DalamudOrange, $"{Service.Lang.GetText("LatestDL")}:");

            ImGui.SameLine();
            ImGui.Text($"{LatestDownloadCounts}");

            ImGui.TextColored(ImGuiColors.DalamudOrange, $"{Service.Lang.GetText("CurrentVersion")}:");

            ImGui.SameLine();
            ImGui.Text($"{CurrentVersion}");

            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.DalamudOrange, $"{Service.Lang.GetText("LatestVersion")}:");

            ImGui.SameLine();
            ImGui.Text($"{LatestVersion}");

            if (ImGui.CollapsingHeader($"{Service.Lang.GetText("Changelog", LatestPublishTime)}:"))
            {
                ImGui.Indent();
                ImGui.TextWrapped($"{LatestChangelog}");
                ImGui.Unindent();
            }

            ImGui.Separator();

            ImGui.TextColored(ImGuiColors.DalamudYellow, $"{Service.Lang.GetText("Settings-TipMessage0")}:");
            ImGui.TextWrapped(Service.Lang.GetText("Settings-TipMessage1"));
            ImGui.TextWrapped(Service.Lang.GetText("Settings-TipMessage2"));

            ImGui.EndTabItem();
        }
    }

    private static void LanguageSwitchHandler(string languageName)
    {
        Service.Config.SelectedLanguage = languageName;
        Service.Lang = new LanguageManager(Service.Config.SelectedLanguage);
        Service.Config.Save();

        ModuleCache.Clear();
        P.CommandHandler();
    }

    private static async Task<int> GetTotalDownloadsAsync(string userName, string repoName)
    {
        var url = $"https://api.github.com/repos/{userName}/{repoName}/releases";
        client.DefaultRequestHeaders.UserAgent.TryParseAdd("request");
        var response = await client.GetStringAsync(url);
        var releases = JsonConvert.DeserializeObject<List<Release>>(response);
        var totalDownloads = 0;
        foreach (var release in releases)
        foreach (var asset in release.assets)
            totalDownloads += asset.download_count * 2;
        return totalDownloads;
    }

    // version - download count - description
    private static async Task<(string version, int downloadCount, string description, string publishTime)>
        GetLatestVersionAsync(string userName, string repoName)
    {
        var url = $"https://api.github.com/repos/{userName}/{repoName}/releases/latest";
        client.DefaultRequestHeaders.UserAgent.TryParseAdd("request");
        var response = await client.GetStringAsync(url);
        var latestRelease = JsonConvert.DeserializeObject<Release>(response);

        var totalDownloads = 0;
        foreach (var asset in latestRelease.assets) totalDownloads += asset.download_count * 2;

        return (latestRelease.tag_name, totalDownloads, latestRelease.body.Replace("- ", "· "),
                   latestRelease.published_at.ToShortDateString());
    }

    private static void GetCurrentVersion()
    {
        CurrentVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
    }

    public void Dispose()
    {
        Service.Config.Save();
    }
}
