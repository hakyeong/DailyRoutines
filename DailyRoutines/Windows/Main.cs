using System;
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
    #region Info
    public class ModuleInfo
    {
        public Type Module { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string Description { get; set; } = null!;
        public ModuleCategories Category { get; set; }
    }

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

    public class VersionInfo
    {
        public Version Version { get; set; } = new();
        public DateTime PublishTime { get; set; } = DateTime.MinValue;
        public string Changelog { get; set; } = string.Empty;
        public int DownloadCount { get; set; }
    }
    #endregion

    private static readonly List<ModuleInfo> Modules = [];
    private static readonly Dictionary<ModuleCategories, List<ModuleInfo>> categorizedModules = new();

    internal static string SearchString = string.Empty;
    private static string ConflictKeySearchString = string.Empty;

    private static readonly HttpClient client = new();
    private static int TotalDownloadCounts;
    private static Version CurrentVersion = new();
    private static VersionInfo LatestVersionInfo = new();

    public Main(Plugin plugin) : base("Daily Routines - Main")
    {
        Flags = ImGuiWindowFlags.NoScrollbar;

        var allModules = Assembly.GetExecutingAssembly().GetTypes()
                                 .Where(t => typeof(DailyModuleBase).IsAssignableFrom(t) && t is { IsClass: true, IsAbstract: false })
                                 .Select(type => new ModuleInfo
                                 {
                                     Module = type,
                                     Title = Service.Lang.GetText(type.GetCustomAttribute<ModuleDescriptionAttribute>()?.TitleKey ?? "DevModuleTitle"),
                                     Description = Service.Lang.GetText(type.GetCustomAttribute<ModuleDescriptionAttribute>()?.DescriptionKey ?? "DevModuleDescription"),
                                     Category = type.GetCustomAttribute<ModuleDescriptionAttribute>()?.Category ?? ModuleCategories.Base
                                 }).ToList();
        
        Modules.AddRange(allModules);

        allModules.GroupBy(m => m.Category).ToList().ForEach(group =>
        {
            categorizedModules[group.Key] = [.. group.OrderBy(m => m.Title)];
        });

        Task.Run(async () =>
        {
            TotalDownloadCounts = await GetTotalDownloadsAsync();
            LatestVersionInfo = await GetLatestVersionAsync("AtmoOmen", "DailyRoutines");
        });

        CurrentVersion = GetCurrentVersion();
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
                foreach (var category in Enum.GetValues(typeof(ModuleCategories)))
                    DrawTabItemModules((ModuleCategories)category);
                DrawTabSettings();
            }
            else
                DrawTabItemModulesSearchResult(Modules);

            ImGui.EndTabBar();
        }
    }

    private static void DrawTabItemModules(ModuleCategories category)
    {
        if (!categorizedModules.TryGetValue(category, out var modules)) return;
        var modulesInCategory = modules.ToArray();

        if (ImGui.BeginTabItem(Service.Lang.GetText(category.ToString())))
        {
            if (ImGui.BeginChild(category.ToString()))
            {
                for (var i = 0; i < modulesInCategory.Length; i++)
                {
                    var module = modulesInCategory[i];

                    ImGui.PushID($"{module.Module.Name}-{module.Category}-{module.Title}-{module.Description}");
                    DrawModuleCheckbox(module, modulesInCategory.Length, i);
                    ImGui.PopID();
                }
                ImGui.EndChild();
            }
            ImGui.EndTabItem();
        }
    }

    private static void DrawTabItemModulesSearchResult(IReadOnlyList<ModuleInfo> modules)
    {
        if (ImGui.BeginTabItem(Service.Lang.GetText("SearchResult")))
        {
            for (var i = 0; i < modules.Count; i++)
            {
                var module = modules[i];
                if (!module.Title.Contains(SearchString, StringComparison.OrdinalIgnoreCase) &&
                    !module.Description.Contains(SearchString, StringComparison.OrdinalIgnoreCase)) continue;

                ImGui.PushID($"{module}");
                DrawModuleCheckbox(module, modules.Count, i);
                ImGui.PopID();
            }

            ImGui.EndTabItem();
        }
    }

    private static void DrawModuleCheckbox(ModuleInfo moduleInfo, int modulesCount, int index)
    {
        var moduleName = moduleInfo.Module.Name;
        if (!Service.Config.ModuleEnabled.TryGetValue(moduleName, out var tempModuleBool))
            return;

        var methodInfo =
            moduleInfo.Module.GetMethod(
                "ConfigUI", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        var isWithUI = methodInfo != null && methodInfo.DeclaringType != typeof(ModuleBase);

        if (ImGuiOm.CheckboxColored("", ref tempModuleBool))
        {
            Service.Config.ModuleEnabled[moduleName] ^= true;

            var component = ModuleManager.Modules[moduleInfo.Module];
            if (tempModuleBool) ModuleManager.Load(component);
            else ModuleManager.Unload(component);

            Service.Config.Save();
        }

        var moduleText = $"[{moduleName}]";
        ImGui.SameLine();
        var origCursorPos = ImGui.GetCursorPosX();
        ImGui.SetCursorPosX(ImGui.GetWindowWidth() - (ImGui.CalcTextSize(moduleText).X * 0.8f) -
                            (4 * ImGui.GetStyle().FramePadding.X));
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
                    DrawModuleUI(moduleInfo);
                    ImGui.EndGroup();
                }
            }
            else
                ImGui.TextColored(ImGuiColors.DalamudYellow, moduleInfo.Title);
        }
        else
            ImGui.Text(moduleInfo.Title);

        ImGui.SetCursorPosX(origCursorPos);
        ImGuiOm.TextDisabledWrapped(moduleInfo.Description);
        if (index < modulesCount - 1) ImGui.Separator();

        return;

        bool CollapsingHeader()
        {
            ImGui.PushStyleColor(ImGuiCol.Text, tempModuleBool ? ImGuiColors.DalamudYellow : ImGuiColors.DalamudWhite);
            ImGui.PushStyleColor(ImGuiCol.Header, ImGui.ColorConvertFloat4ToU32(new Vector4(0)));
            var collapsingHeader = ImGui.CollapsingHeader(moduleInfo.Title);
            ImGui.PopStyleColor(2);

            return collapsingHeader;
        }
    }

    private static void DrawModuleUI(ModuleInfo moduleInfo)
    {
        var moduleInstance = ModuleManager.Modules[moduleInfo.Module];

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
            ImGui.BeginDisabled();
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

            ImGui.EndDisabled();

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
            ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.DalamudGrey3);
            if (ImGui.Button("GitHub")) Util.OpenLink("https://github.com/AtmoOmen/DailyRoutines");
            ImGui.PopStyleColor();
            
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.ParsedPink);
            if (ImGui.Button("bilibili")) Util.OpenLink("https://www.bilibili.com/read/cv31823881/");
            ImGui.PopStyleColor();

            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.TankBlue);
            if (ImGui.Button("QQ 群")) Util.OpenLink("https://qm.qq.com/q/QlImB8pn2");
            ImGui.PopStyleColor();
            ImGuiOm.TooltipHover("951926472");

            ImGui.Separator();

            ImGui.TextColored(ImGuiColors.DalamudYellow, $"{Service.Lang.GetText("Info")}:");

            ImGui.SameLine();
            if (ImGui.SmallButton(Service.Lang.GetText("Refresh")))
            {
                Task.Run(async () =>
                {
                    TotalDownloadCounts = await GetTotalDownloadsAsync();
                    LatestVersionInfo = await GetLatestVersionAsync("AtmoOmen", "DailyRoutines");
                });
            }

            ImGui.TextColored(ImGuiColors.DalamudOrange, $"{Service.Lang.GetText("TotalDL")}:");

            ImGui.SameLine();
            ImGui.Text($"{TotalDownloadCounts}");

            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.DalamudOrange, $"{Service.Lang.GetText("LatestDL")}:");

            ImGui.SameLine();
            ImGui.Text($"{LatestVersionInfo.DownloadCount}");

            ImGui.TextColored(ImGuiColors.DalamudOrange, $"{Service.Lang.GetText("CurrentVersion")}:");

            ImGui.SameLine();
            ImGui.Text($"{CurrentVersion}");

            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.DalamudOrange, $"{Service.Lang.GetText("LatestVersion")}:");

            ImGui.SameLine();
            ImGui.Text($"{LatestVersionInfo.Version}");

            if (ImGui.CollapsingHeader($"{Service.Lang.GetText("Changelog", LatestVersionInfo.PublishTime.ToShortDateString())}:"))
            {
                ImGui.Indent();
                ImGui.TextWrapped(LatestVersionInfo.Changelog);
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

        P.CommandHandler();
    }

    private static async Task<int> GetTotalDownloadsAsync()
    {
        const string url = "https://raw.gitmirror.com/AtmoOmen/DailyRoutines/main/downloads.txt";
        var response = await client.GetStringAsync(url);
        return int.TryParse(response, out var totalDownloads) ? totalDownloads : 0;
    }

    // version - download count - description
    private static async Task<VersionInfo> GetLatestVersionAsync(string userName, string repoName)
    {
        var url = $"https://api.github.com/repos/{userName}/{repoName}/releases/latest";
        client.DefaultRequestHeaders.UserAgent.TryParseAdd("request");
        var response = await client.GetStringAsync(url);
        var latestRelease = JsonConvert.DeserializeObject<Release>(response);

        var totalDownloads = 0;
        var version = new VersionInfo();
        foreach (var asset in latestRelease.assets) totalDownloads += asset.download_count * 2;

        version.Version = new Version(latestRelease.tag_name);
        version.PublishTime = latestRelease.published_at;
        version.Changelog = latestRelease.body;
        version.DownloadCount = totalDownloads;

        return version;
    }

    private static Version GetCurrentVersion() => Assembly.GetExecutingAssembly().GetName().Version;

    public void Dispose()
    {
        Service.Config.Save();
    }
}
