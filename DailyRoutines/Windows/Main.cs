using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Reflection;
using System.Security.Policy;
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

    public class VersionInfo
    {
        public Version Version { get; set; } = new();
        public DateTime PublishTime { get; set; } = DateTime.MinValue;
        public string Changelog { get; set; } = string.Empty;
        public int DownloadCount { get; set; }
    }

    public class GameEvent
    {
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public DateTime BeginTime { get; set; } = DateTime.MinValue;
        public DateTime EndTime { get; set; } = DateTime.MaxValue;
        public Vector4 Color { get; set; }
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
    private static List<GameEvent> GameCalendars = new();


    public Main(Plugin plugin) : base("Daily Routines - Main")
    {
        Flags = ImGuiWindowFlags.NoScrollbar;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new(650, 300)};

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

        ObtainNecessityInfo();
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
                    DrawModules((ModuleCategories)category);
                DrawTabSettings();
            }
            else
                DrawModulesSearchResult(Modules);

            ImGui.EndTabBar();
        }
    }

    private static void DrawModules(ModuleCategories category)
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
                    DrawModuleUI(module, modulesInCategory.Length, i);
                    ImGui.PopID();
                }
                ImGui.EndChild();
            }
            ImGui.EndTabItem();
        }
    }

    private static void DrawModulesSearchResult(IReadOnlyList<ModuleInfo> modules)
    {
        if (ImGui.BeginTabItem(Service.Lang.GetText("SearchResult")))
        {
            for (var i = 0; i < modules.Count; i++)
            {
                var module = modules[i];
                if (!module.Title.Contains(SearchString, StringComparison.OrdinalIgnoreCase) &&
                    !module.Description.Contains(SearchString, StringComparison.OrdinalIgnoreCase)) continue;

                ImGui.PushID($"{module.Category}-{module.Description}-{module.Title}-{module.Module}");
                DrawModuleUI(module, modules.Count, i);
                ImGui.PopID();
            }

            ImGui.EndTabItem();
        }
    }

    private static void DrawModuleUI(ModuleInfo moduleInfo, int modulesCount, int index)
    {
        var moduleName = moduleInfo.Module.Name;
        if (!Service.Config.ModuleEnabled.TryGetValue(moduleName, out var tempModuleBool))
            return;

        var methodInfo =
            moduleInfo.Module.GetMethod(
                "ConfigUI", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        var isWithUI = methodInfo != null && methodInfo.DeclaringType != typeof(ModuleBase);

        var component = ModuleManager.Modules[moduleInfo.Module];
        if (ImGuiOm.CheckboxColored("", ref tempModuleBool))
        {
            Service.Config.ModuleEnabled[moduleName] ^= true;

            if (tempModuleBool) ModuleManager.Load(component);
            else ModuleManager.Unload(component);

            Service.Config.Save();
        }

        var moduleText = $"[{moduleName}]";
        ImGui.SameLine();
        var origCursorPosX = ImGui.GetCursorPosX();
        ImGui.SetCursorPosX(ImGui.GetWindowWidth() - (ImGui.CalcTextSize(moduleText).X * 0.8f) - (4 * ImGui.GetStyle().FramePadding.X));
        ImGui.SetWindowFontScale(0.8f);
        ImGui.TextDisabled(moduleText);
        ImGui.SetWindowFontScale(1f);

        var author = component.Author;
        var isWithAuthor = !string.IsNullOrEmpty(author);

        if (isWithAuthor)
        {
            ImGui.SameLine();
            ImGui.SetCursorPosX(origCursorPosX + ImGui.CalcTextSize(moduleInfo.Title).X + ImGui.GetStyle().FramePadding.X * 8 + (tempModuleBool && isWithUI ? 20f : -15f));
            ImGui.TextColored(ImGuiColors.DalamudGrey3, $"{Service.Lang.GetText("Author")}: {author}");
        }

        ImGui.SameLine();
        ImGui.SetCursorPosX(origCursorPosX);

        if (tempModuleBool)
        {
            if (isWithUI)
            {
                if (CollapsingHeader())
                {
                    ImGui.SetCursorPosX(origCursorPosX);
                    ImGui.BeginGroup();
                    component.ConfigUI();
                    ImGui.EndGroup();
                }
            }
            else
                ImGui.TextColored(ImGuiColors.DalamudYellow, moduleInfo.Title);
        }
        else
            ImGui.Text(moduleInfo.Title);

        ImGui.SetCursorPosX(origCursorPosX);
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

            ImGuiOm.TextIcon(FontAwesomeIcon.FolderOpen, Service.Lang.GetText("ModulesConfig"));

            ImGui.SameLine();
            if (ImGui.Button(Service.Lang.GetText("OpenFolder")))
            {
                HelpersOm.OpenFolder(P.PluginInterface.ConfigDirectory.FullName);
            }

            ImGuiOm.TooltipHover(Service.Lang.GetText("ModulesConfigHelp"));

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

                var validKeys = Service.KeyState.GetValidVirtualKeys();
                foreach (var keyToSelect in validKeys)
                {
                    if (!string.IsNullOrWhiteSpace(ConflictKeySearchString) && !keyToSelect.GetFancyName()
                            .Contains(ConflictKeySearchString, StringComparison.OrdinalIgnoreCase)) continue;
                    if (ImGui.Selectable(keyToSelect.GetFancyName()))
                    {
                        Service.Config.ConflictKey = keyToSelect;
                        Service.Config.Save();
                    }
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

            ImGui.SameLine(0f, 16f);
            ImGui.PushStyleColor(ImGuiCol.Button, ImGuiColors.ParsedPurple);
            if (ImGui.Button("爱发电")) Util.OpenLink("https://afdian.net/a/AtmoOmen");
            ImGui.PopStyleColor();
            ImGuiOm.TooltipHover(Service.Lang.GetText("DonateHelp"));

            ImGui.Separator();

            ImGui.TextColored(ImGuiColors.DalamudYellow, $"{Service.Lang.GetText("Info")}:");

            ImGui.SameLine();
            if (ImGui.SmallButton(Service.Lang.GetText("Refresh")))
                ObtainNecessityInfo();

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

            ImGui.TextColored(ImGuiColors.DalamudYellow, $"{Service.Lang.GetText("GameCalendar")}:");

            if (GameCalendars is { Count: > 0 })
            {
                var longestText = string.Empty;
                foreach (var activity in GameCalendars)
                {
                    if (activity.Name.Length > longestText.Length) longestText = activity.Name;
                }

                var buttonSize = ImGui.CalcTextSize($"前缀俩字{longestText}后缀俩字");
                var framePadding = ImGui.GetStyle().FramePadding;
                foreach (var activity in GameCalendars)
                {
                    ImGui.PushStyleColor(ImGuiCol.Button, activity.Color);
                    if (ImGui.Button($"{activity.Name}###{activity.Url}", new(8 * framePadding.X + buttonSize.X, 2 * framePadding.Y + buttonSize.Y)))
                    {
                        Util.OpenLink($"{activity.Url}");
                    }
                    ImGui.PopStyleColor();

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.TextColored(ImGuiColors.DalamudOrange, $"{Service.Lang.GetText("StartTime")}: ");

                        ImGui.SameLine();
                        ImGui.Text($"{activity.BeginTime}");

                        ImGui.SameLine();
                        ImGui.TextColored(activity.BeginTime > DateTime.Now ? ImGuiColors.DPSRed : ImGuiColors.HealerGreen, activity.BeginTime > DateTime.Now ? Service.Lang.GetText("GameCalendar-EventNotStarted") : Service.Lang.GetText("GameCalendar-EventStarted"));

                        ImGui.TextColored(ImGuiColors.DalamudOrange, $"{Service.Lang.GetText("EndTime")}: ");

                        ImGui.SameLine();
                        ImGui.Text($"{activity.EndTime}");

                        ImGui.SameLine();
                        ImGui.TextColored(activity.EndTime < DateTime.Now ? ImGuiColors.DPSRed : ImGuiColors.HealerGreen, activity.EndTime > DateTime.Now ? Service.Lang.GetText("GameCalendar-EventNotEnded") : Service.Lang.GetText("GameCalendar-EventEnded"));

                        ImGui.EndTooltip();
                    }
                }
            }

            ImGui.Separator();

            ImGui.TextColored(ImGuiColors.DalamudYellow, $"{Service.Lang.GetText("Settings-TipMessage0")}:");
            ImGui.TextWrapped(Service.Lang.GetText("Settings-TipMessage1"));
            ImGui.TextWrapped(Service.Lang.GetText("Settings-TipMessage2"));

            ImGui.EndTabItem();
        }
    }

    private static void DrawTabGameInfo()
    {
        if (ImGui.BeginTabItem("游戏信息"))
        {

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

    private static void ObtainNecessityInfo()
    {
        Task.Run(async () =>
        {
            await GetGameCalendar();
            TotalDownloadCounts = await GetTotalDownloadsAsync();
            LatestVersionInfo = await GetLatestVersionAsync("AtmoOmen", "DailyRoutines");
        });

        CurrentVersion = GetCurrentVersion();
    }

    private static async Task<int> GetTotalDownloadsAsync()
    {
        const string url = "https://mirror.ghproxy.com/https://raw.githubusercontent.com/AtmoOmen/DailyRoutines/main/downloads.txt";
        var response = await client.GetStringAsync(url);
        return int.TryParse(response, out var totalDownloads) ? totalDownloads : 0;
    }

    // version - download count - description
    private static async Task<VersionInfo> GetLatestVersionAsync(string userName, string repoName)
    {
        var url = $"https://api.github.com/repos/{userName}/{repoName}/releases/latest";
        client.DefaultRequestHeaders.UserAgent.TryParseAdd("request");
        var response = await client.GetStringAsync(url);
        var latestRelease = JsonConvert.DeserializeObject<FileFormat.GitHubRelease>(response);

        var totalDownloads = 0;
        var version = new VersionInfo();
        foreach (var asset in latestRelease.assets) totalDownloads += asset.download_count * 2;

        version.Version = new Version(latestRelease.tag_name);
        version.PublishTime = latestRelease.published_at;
        version.Changelog = latestRelease.body;
        version.DownloadCount = totalDownloads;

        return version;
    }

    private static async Task GetGameCalendar()
    {
        const string url = "https://apiff14risingstones.web.sdo.com/api/home/active/calendar/getActiveCalendarMonth";
        var response = await client.GetStringAsync(url);
        var result = JsonConvert.DeserializeObject<FileFormat.RSActivityCalendar>(response);

        if (result.data.Count > 0)
            foreach (var activity in result.data)
            {
                var gameEvent = new GameEvent
                {
                    Name = activity.name,
                    Url = activity.url,
                    BeginTime = HelpersOm.UnixSecondToDateTime(activity.begin_time),
                    EndTime = HelpersOm.UnixSecondToDateTime(activity.end_time),
                    Color = HelpersOm.DarkenColor(HelpersOm.HexToVector4(activity.color), 0.3f)
                };
                GameCalendars.Add(gameEvent);
            }
    }

    private static Version GetCurrentVersion() => Assembly.GetExecutingAssembly().GetName().Version;

    public void Dispose()
    {
        Service.Config.Save();
    }
}
