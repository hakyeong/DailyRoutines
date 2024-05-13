using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Reflection;
using System.Threading.Tasks;
using DailyRoutines.Helpers;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using DailyRoutines.Modules;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using ImGuiNET;
using Newtonsoft.Json;

namespace DailyRoutines.Windows;

public class Main : Window, IDisposable
{
    public class ModuleInfo
    {
        public Type Module { get; set; } = null!;
        public string[]? PrecedingModule { get; set; }
        public string Title { get; set; } = null!;
        public string Description { get; set; } = null!;
        public string? Author { get; set; }
        public bool WithConfigUI { get; set; }
        public ModuleCategories Category { get; set; }
    }

    private static readonly List<ModuleInfo> Modules = [];
    private static readonly Dictionary<ModuleCategories, List<ModuleInfo>> categorizedModules = [];

    internal static string SearchString = string.Empty;

    public Main() : base("Daily Routines - 主界面")
    {
        Flags = ImGuiWindowFlags.NoScrollbar;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new(650, 300) };

        var allModules = Assembly.GetExecutingAssembly().GetTypes()
                                 .Where(t => typeof(DailyModuleBase).IsAssignableFrom(t) &&
                                             t is { IsClass: true, IsAbstract: false })
                                 .Select(type => new ModuleInfo
                                 {
                                     Module = type,
                                     PrecedingModule = type.GetCustomAttribute<PrecedingModuleAttribute>()?.Modules
                                                           .Select(t => t.Name + "Title")
                                                           .Select(title => Service.Lang.GetText(title))
                                                           .ToArray(),
                                     Title = Service.Lang.GetText(
                                         type.GetCustomAttribute<ModuleDescriptionAttribute>()?.TitleKey ??
                                         "DevModuleTitle"),
                                     Description = Service.Lang.GetText(
                                         type.GetCustomAttribute<ModuleDescriptionAttribute>()?.DescriptionKey ??
                                         "DevModuleDescription"),
                                     Category = type.GetCustomAttribute<ModuleDescriptionAttribute>()?.Category ??
                                                ModuleCategories.一般,
                                     Author = ((DailyModuleBase)Activator.CreateInstance(type)!).Author,
                                     WithConfigUI = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                                                        .Any(m => m.Name == "ConfigUI" && 
                                                                  m.DeclaringType != typeof(DailyModuleBase))
                                 })
                                 .ToList();

        Modules.AddRange(allModules);
        allModules.GroupBy(m => m.Category).ToList().ForEach(group =>
        {
            categorizedModules[group.Key] =
                [.. group.OrderBy(m => m.Title)];
        });

        MainSettings.Init();
    }

    public override void Draw()
    {
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##MainWindow-SearchInput", $"{Service.Lang.GetText("PleaseSearch")}...",
                                ref SearchString, 100);
        ImGui.Separator();

        if (ImGui.BeginTabBar("BasicTab", ImGuiTabBarFlags.FittingPolicyScroll))
        {
            if (string.IsNullOrEmpty(SearchString))
            {
                foreach (var category in Enum.GetValues(typeof(ModuleCategories)))
                    DrawModules((ModuleCategories)category);
                MainSettings.Draw();
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

        if (ImGui.BeginTabItem(category.ToString()))
        {
            if (ImGui.BeginChild(category.ToString()))
            {
                for (var i = 0; i < modulesInCategory.Length; i++)
                {
                    var module = modulesInCategory[i];

                    ImGui.PushID($"{module.Module.Name}-{module.Category}-{module.Title}-{module.Description}");
                    DrawModuleUI(module, modulesInCategory.Length, i, false);
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
                    !module.Description.Contains(SearchString, StringComparison.OrdinalIgnoreCase) &&
                    !module.Module.Name.Contains(SearchString, StringComparison.OrdinalIgnoreCase)) continue;

                ImGui.PushID($"{module.Category}-{module.Description}-{module.Title}-{module.Module}");
                DrawModuleUI(module, modules.Count, i, true);
                ImGui.PopID();
            }

            ImGui.EndTabItem();
        }
    }

    private static void DrawModuleUI(ModuleInfo moduleInfo, int modulesCount, int index, bool fromSearch)
    {
        var moduleName = moduleInfo.Module.Name;
        if (!Service.Config.ModuleEnabled.TryGetValue(moduleName, out var isModuleEnabled)) return;
            
        if (ImGuiOm.CheckboxColored("", ref isModuleEnabled))
        {
            Service.Config.ModuleEnabled[moduleName] ^= true;

            var module = Service.ModuleManager.Modules[moduleInfo.Module];
            if (isModuleEnabled) Service.ModuleManager.Load(module);
            else Service.ModuleManager.Unload(module);

            Service.Config.Save();
        }

        if (fromSearch) ImGuiOm.TooltipHover(moduleInfo.Category.ToString());

        var moduleText = $"[{moduleName}]";
        ImGui.SameLine();
        var origCursorPosX = ImGui.GetCursorPosX();
        ImGui.SetCursorPosX(ImGui.GetWindowWidth() - (ImGui.CalcTextSize(moduleText).X * 0.8f) -
                            (4 * ImGui.GetStyle().FramePadding.X));
        ImGui.SetWindowFontScale(0.8f);
        ImGui.TextDisabled(moduleText);
        ImGui.SetWindowFontScale(1f);

        var isWithAuthor = !string.IsNullOrEmpty(moduleInfo.Author);
        if (isWithAuthor)
        {
            ImGui.SameLine();
            ImGui.SetCursorPosX(origCursorPosX + ImGui.CalcTextSize(moduleInfo.Title).X +
                                (ImGui.GetStyle().FramePadding.X * 8) +
                                (isModuleEnabled && moduleInfo.WithConfigUI ? 20f : -15f));
            ImGui.TextColored(ImGuiColors.DalamudGrey3, $"{Service.Lang.GetText("Author")}: {moduleInfo.Author}");
        }

        ImGui.SameLine();
        ImGui.SetCursorPosX(origCursorPosX);

        if (isModuleEnabled)
        {
            if (moduleInfo.WithConfigUI)
            {
                if (CollapsingHeader())
                {
                    ImGui.SetCursorPosX(origCursorPosX);
                    ImGui.BeginGroup();
                    Service.ModuleManager.Modules[moduleInfo.Module].ConfigUI();
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

        ImGui.SetCursorPosX(origCursorPosX);
        ImGui.BeginGroup();
        if (moduleInfo.PrecedingModule is { Length: > 0 })
        {
            ImGuiOm.TextDisabledWrapped($"({Service.Lang.GetText("PrecedingModules")}:");
            for (var i = 0; i < moduleInfo.PrecedingModule.Length; i++)
            {
                var pModule = moduleInfo.PrecedingModule[i];

                ImGui.SameLine();
                ImGui.TextColored(ImGuiColors.DalamudYellow, pModule);

                if (ImGui.IsItemClicked())
                    SearchString = pModule;

                if (i < moduleInfo.PrecedingModule.Length - 1)
                {
                    ImGui.SameLine();
                    ImGui.TextDisabled("/");
                }
            }
            ImGui.SameLine(0, 0);
            ImGuiOm.TextDisabledWrapped(")");
        }
        ImGui.EndGroup();

        if (index < modulesCount - 1) ImGui.Separator();

        return;

        bool CollapsingHeader()
        {
            ImGui.PushStyleColor(ImGuiCol.Text, isModuleEnabled ? ImGuiColors.DalamudYellow : ImGuiColors.DalamudWhite);
            ImGui.PushStyleColor(ImGuiCol.Header, ImGui.ColorConvertFloat4ToU32(new Vector4(0)));
            var collapsingHeader = ImGui.CollapsingHeader(moduleInfo.Title);
            ImGui.PopStyleColor(2);

            return collapsingHeader;
        }
    }

    public void Dispose()
    {
        MainSettings.Uninit();
        Service.Config.Save();
    }
}

public class MainSettings
{
    public class VersionInfo
    {
        public Version Version { get; set; } = new();
        public DateTime PublishTime { get; set; } = DateTime.MinValue;
        public string Changelog { get; set; } = string.Empty;
        public int DownloadCount { get; set; }
    }

    public class GameEvent
    {
        public uint ID { get; set; }
        public DalamudLinkPayload? LinkPayload { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public DateTime BeginTime { get; set; } = DateTime.MinValue;
        public DateTime EndTime { get; set; } = DateTime.MaxValue;
        public Vector4 Color { get; set; }
        /// <summary>
        /// 0 - 正在进行; 1 - 未开始; 2 - 已结束
        /// </summary>
        public uint State { get; set; }
        /// <summary>
        /// 如果已结束, 则为 -1
        /// </summary>
        public int DaysLeft { get; set; } = int.MaxValue;
    }

    public class GameNews
    {
        public string Title { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string PublishDate { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string HomeImagePath { get; set; } = string.Empty;
        public int SortIndex { get; set; }
    }

    private static string ConflictKeySearchString = string.Empty;
    private static readonly HttpClient client = new();
    private static int TotalDownloadCounts;
    private static VersionInfo LatestVersionInfo = new();
    private static List<GameEvent> GameCalendars = [];
    private static readonly List<GameNews> GameNewsList = [];

    public static void Init()
    {
        ObtainNecessityInfo();
        Service.ClientState.Login += OnLogin;
    }

    internal static void Draw()
    {
        if (ImGui.BeginTabItem(Service.Lang.GetText("Settings")))
        {
            DrawGlobalConfig();
            ImGui.Separator();

            DrawContact();
            ImGui.Separator();

            DrawPluginStats();
            ImGui.Separator();

            ImGui.BeginGroup();
            DrawGameEventsCalendar();
            ImGui.EndGroup();

            ImGui.SameLine();
            ImGui.Dummy(new(48));

            ImGui.SameLine();
            ImGui.BeginGroup();
            DrawGameNews();
            ImGui.EndGroup();
            ImGui.Separator();
            
            DrawTooltips();

            ImGui.EndTabItem();
        }
    }

    private static void DrawGlobalConfig()
    {
        // 第一列
        ImGui.BeginGroup();
        ImGuiOm.TextIcon(FontAwesomeIcon.Globe, $"{Service.Lang.GetText("Language")}:");

        ImGui.SameLine();
        ImGui.BeginDisabled();
        ImGui.SetNextItemWidth(180f * ImGuiHelpers.GlobalScale);
        if (ImGui.BeginCombo("##LanguagesList", "简体中文")) ImGui.EndCombo();

        ImGui.EndDisabled();

        ImGuiOm.TextIcon(FontAwesomeIcon.FolderOpen, Service.Lang.GetText("ModulesConfig"));

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("OpenFolder")))
            OpenFolder(Service.PluginInterface.ConfigDirectory.FullName);

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

        ImGuiOm.TextIcon(FontAwesomeIcon.Database, Service.Lang.GetText("Settings-AllowAnonymousUpload"));

        ImGui.SameLine();
        var allowState = Service.Config.AllowAnonymousUpload;
        if (ImGui.Checkbox("###AllowAnonymousUpload", ref allowState))
        {
            Service.Config.AllowAnonymousUpload ^= true;
            Service.Config.Save();

            if (Service.Config.AllowAnonymousUpload)
                Task.Run(() => OnlineStatsManager.UploadEntry(new OnlineStatsManager.ModulesState(OnlineStatsManager.GetEncryptedMachineCode())));
        }

        ImGuiOm.HelpMarker(Service.Lang.GetText("Settings-AllowAnonymousUploadHelp"), 25f);

        ImGui.EndGroup();
    }

    private static void DrawContact()
    {
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
        if (ImGui.Button("bilibili")) Util.OpenLink("https://space.bilibili.com/22008977");
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
    }

    private static void DrawPluginStats()
    {
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
        ImGui.TextColored(Plugin.Version < LatestVersionInfo.Version ? ImGuiColors.DPSRed : ImGuiColors.DalamudWhite, $"{Plugin.Version}");

        if (Plugin.Version < LatestVersionInfo.Version)
            ImGuiOm.TooltipHover(Service.Lang.GetText("LowVersionWarning"));

        ImGui.SameLine();
        ImGui.TextColored(ImGuiColors.DalamudOrange, $"{Service.Lang.GetText("LatestVersion")}:");

        ImGui.SameLine();
        ImGui.Text($"{LatestVersionInfo.Version}");

        if (ImGui.CollapsingHeader(
                $"{Service.Lang.GetText("Changelog", LatestVersionInfo.PublishTime.ToShortDateString())}:"))
        {
            ImGui.Indent();
            var imageState = ImageHelper
                .TryGetImage("https://gh.atmoomen.top/DailyRoutines/main/Assets/Images/Changelog.png", out var imageHandle);

            if (imageState)
                ImGui.Image(imageHandle.ImGuiHandle, imageHandle.Size * 0.8f);
            else
                ImGui.TextDisabled($"{Service.Lang.GetText("ImageLoading")}...");
            ImGui.Unindent();
        }
    }

    private static void DrawGameEventsCalendar()
    {
        ImGui.TextColored(ImGuiColors.DalamudYellow, $"{Service.Lang.GetText("GameCalendar")}:");

        ImGui.SameLine();
        if (ImGui.SmallButton(Service.Lang.GetText("Settings")))
            ImGui.OpenPopup("GameCalendarSettings");

        if (ImGui.BeginPopup("GameCalendarSettings"))
        {
            var checkboxBool = Service.Config.SendCalendarToChatWhenLogin;
            if (ImGui.Checkbox(Service.Lang.GetText("Settings-SendCalendarToCharWhenLogin"), ref checkboxBool))
            {
                Service.Config.SendCalendarToChatWhenLogin ^= true;
                Service.Config.Save();
            }

            var checkboxBool2 = Service.Config.IsHideOutdatedEvent;
            if (ImGui.Checkbox(Service.Lang.GetText("Settings-HideOutdatedEvents"), ref checkboxBool2))
            {
                Service.Config.IsHideOutdatedEvent ^= true;
                Service.Config.Save();
            }

            ImGui.EndPopup();
        }

        ImGui.Spacing();

        if (GameCalendars is { Count: > 0 })
        {
            var longestText = string.Empty;
            foreach (var activity in GameCalendars)
                if (activity.Name.Length > longestText.Length)
                    longestText = activity.Name;

            var buttonSize = ImGui.CalcTextSize($"前缀得五字{longestText}后缀也五字");
            var framePadding = ImGui.GetStyle().FramePadding;
            foreach (var activity in GameCalendars)
            {
                if (Service.Config.IsHideOutdatedEvent && activity.State == 2) continue;
                var statusStr = activity.State == 2 ? Service.Lang.GetText("GameCalendar-EventEnded") : "";
                ImGui.PushStyleColor(ImGuiCol.Button, activity.Color);
                ImGui.BeginDisabled(activity.State == 2);
                if (ImGui.Button($"{activity.Name} {statusStr}###{activity.Url}", buttonSize with { Y = (2 * framePadding.Y) + buttonSize.Y }))
                    Util.OpenLink($"{activity.Url}");
                ImGui.EndDisabled();
                ImGui.PopStyleColor();

                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.TextColored(ImGuiColors.DalamudOrange, "距离");

                    ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.HealerGreen, $"{(activity.State is 0 ? Service.Lang.GetText("End") : Service.Lang.GetText("Start"))}");

                    ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.DalamudOrange, "还有: ");

                    ImGui.SameLine();
                    ImGui.Text($"{activity.DaysLeft} 天");

                    ImGui.TextColored(ImGuiColors.DalamudOrange, activity.State is 0 ? $"{Service.Lang.GetText("EndTime")}: " : $"{Service.Lang.GetText("StartTime")}: ");

                    ImGui.SameLine();
                    ImGui.Text(activity.State is 0 ? $"{activity.EndTime}" : $"{activity.BeginTime}");

                    /*ImGui.SameLine();
                    ImGui.TextColored(
                        activity.BeginTime > DateTime.Now ? ImGuiColors.DPSRed : ImGuiColors.HealerGreen,
                        activity.BeginTime > DateTime.Now
                            ? Service.Lang.GetText("GameCalendar-EventNotStarted")
                            : Service.Lang.GetText("GameCalendar-EventStarted"));

                    ImGui.TextColored(ImGuiColors.DalamudOrange, $"{Service.Lang.GetText("EndTime")}: ");

                    ImGui.SameLine();
                    ImGui.Text($"{activity.EndTime}");

                    ImGui.SameLine();
                    ImGui.TextColored(
                        activity.EndTime < DateTime.Now ? ImGuiColors.DPSRed : ImGuiColors.HealerGreen,
                        activity.EndTime > DateTime.Now
                            ? Service.Lang.GetText("GameCalendar-EventNotEnded")
                            : Service.Lang.GetText("GameCalendar-EventEnded"));*/

                    ImGui.EndTooltip();
                }
            }
        }
    }

    private static void DrawGameNews()
    {
        ImGui.TextColored(ImGuiColors.DalamudYellow, $"{Service.Lang.GetText("GameNews")}:");
        ImGui.Spacing();
        if (GameNewsList is { Count: > 0 })
        {
            var longestText = string.Empty;
            foreach (var news in GameNewsList)
                if (news.Title.Length > longestText.Length)
                    longestText = news.Title;

            var buttonSize = ImGui.CalcTextSize($"前三字{longestText}后三字");
            var framePadding = ImGui.GetStyle().FramePadding;

            foreach (var news in GameNewsList)
            {
                if (ImGui.Button($"{news.Title}###{news.Url}", buttonSize with { Y = (2 * framePadding.Y) + buttonSize.Y }))
                    Util.OpenLink($"{news.Url}");

                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    var imageState = ImageHelper.TryGetImage(news.HomeImagePath, out var imageHandle);
                    if (imageState)
                        ImGui.Image(imageHandle.ImGuiHandle, imageHandle.Size);
                    else
                        ImGui.TextDisabled($"{Service.Lang.GetText("ImageLoading")}...");

                    ImGui.Separator();

                    ImGui.PushTextWrapPos(imageState ? imageHandle.Width + 10f : 400f);
                    ImGui.Text(news.Summary);

                    ImGui.Text($"({Service.Lang.GetText("PublishTime")}: {news.PublishDate})");
                    ImGui.PopTextWrapPos();
                    ImGui.EndTooltip();
                }
            }
        }
    }

    private static void DrawTooltips()
    {
        ImGui.TextColored(ImGuiColors.DalamudYellow, $"{Service.Lang.GetText("Settings-TipMessage0")}:");
        ImGui.TextWrapped(Service.Lang.GetText("Settings-TipMessage1"));
        ImGui.TextWrapped(Service.Lang.GetText("Settings-TipMessage2"));
    }

    internal static void ObtainNecessityInfo()
    {
        Task.Run(async () =>
        {
            ImageHelper.TryGetImage("https://gh.atmoomen.top/DailyRoutines/main/Assets/Images/Changelog.png", out _);
            await GetGameCalendar();
            await GetGameNews();
            TotalDownloadCounts = await GetTotalDownloadsAsync();
            LatestVersionInfo = await GetLatestVersionAsync("AtmoOmen", "DailyRoutines");
        });
    }

    internal static void OnLogin()
    {
        if (!Service.Config.SendCalendarToChatWhenLogin) return;
        if (GameCalendars.Any(x => x.BeginTime <= DateTime.Now && DateTime.Now <= x.EndTime))
        {
            Service.Chat.Print(new SeStringBuilder()
                               .AddUiForeground("[Daily Routines]", 34)
                               .AddUiForeground(
                                   $" {DateTime.Now.ToShortDateString()} {Service.Lang.GetText("GameCalendar")}", 2)
                               .Build());
            var orderNumber = 1;
            foreach (var gameEvent in GameCalendars)
            {
                if (gameEvent.State != 0) continue;
                var message = new SeStringBuilder().AddUiForeground($"{orderNumber}. ", 2)
                                                   .Add(gameEvent.LinkPayload)
                                                   .AddUiForeground($"{gameEvent.Name}", 25)
                                                   .Add(RawPayload.LinkTerminator)
                                                   .AddUiForeground(
                                                       $" ({Service.Lang.GetText("GameCalendar-EndTimeMessage",
                                                           gameEvent.DaysLeft)})", 2)
                                                   .Build();
                Service.Chat.Print(message);
                orderNumber++;
            }
        }
    }

    private static async Task<int> GetTotalDownloadsAsync()
    {
        const string url = "https://gh.atmoomen.top/DailyRoutines/main/Assets/downloads.txt";
        var response = await client.GetStringAsync(url);
        return int.TryParse(response, out var totalDownloads) ? totalDownloads : 0;
    }

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
        {
            GameCalendars.Clear();
            foreach (var activity in result.data)
            {
                var currentTime = DateTime.Now;
                var beginTime = UnixSecondToDateTime(activity.begin_time);
                var endTime = UnixSecondToDateTime(activity.end_time);
                var gameEvent = new GameEvent
                {
                    ID = activity.id,
                    LinkPayload = Service.PluginInterface.AddChatLinkHandler(activity.id, OpenGameEventLinkPayload),
                    Name = activity.name,
                    Url = activity.url,
                    BeginTime = beginTime,
                    EndTime = endTime,
                    Color = DarkenColor(HexToVector4(activity.color), 0.3f),
                    State = (currentTime < beginTime) ? 1U :
                            (currentTime <= endTime) ? 0U : 2U,
                    DaysLeft = (currentTime < beginTime) ? (beginTime - DateTime.Now).Days :
                               (currentTime <= endTime) ? (endTime - DateTime.Now).Days : int.MaxValue,
                };
                GameCalendars.Add(gameEvent);
            }

            GameCalendars = [..GameCalendars.OrderBy(x => x.DaysLeft)];
        }
    }

    private static void OpenGameEventLinkPayload(uint commandID, SeString message)
    {
        var link = GameCalendars.FirstOrDefault(x => x.ID == commandID)?.Url;
        if (!string.IsNullOrWhiteSpace(link))
            Util.OpenLink(link);
    }

    private static async Task GetGameNews()
    {
        const string url =
            "https://cqnews.web.sdo.com/api/news/newsList?gameCode=ff&CategoryCode=5309,5310,5311,5312,5313&pageIndex=0&pageSize=5";
        var response = await client.GetStringAsync(url);
        var result = JsonConvert.DeserializeObject<FileFormat.RSGameNews>(response);

        if (result.Data.Count > 0)
        {
            GameNewsList.Clear();
            foreach (var activity in result.Data)
            {
                var gameNews = new GameNews()
                {
                    Title = activity.Title,
                    Url = activity.Author,
                    SortIndex = activity.SortIndex,
                    Summary = activity.Summary,
                    HomeImagePath = activity.HomeImagePath,
                    PublishDate = activity.PublishDate
                };
                GameNewsList.Add(gameNews);
            }
        }
    }

    public static void Uninit()
    {
        Service.ClientState.Login -= OnLogin;
        foreach (var gameEvent in GameCalendars)
            Service.PluginInterface.RemoveChatLinkHandler(gameEvent.ID);
    }
}
