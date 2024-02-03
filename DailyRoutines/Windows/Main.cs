using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using ClickLib;
using DailyRoutines.Infos;
using DailyRoutines.Manager;
using DailyRoutines.Managers;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using Dalamud.Memory;
using Dalamud.Utility;
using ECommons.Reflection;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using Map = Lumina.Excel.GeneratedSheets.Map;

namespace DailyRoutines.Windows;

public class Main : Window, IDisposable
{
    private static readonly ConcurrentDictionary<Type, (string Name, string Title, string Description)> ModuleCache =
        new();

    private static readonly Dictionary<ModuleCategories, List<Type>> ModuleCategories = new();
    internal static string SearchString = string.Empty;
    private static string ConflictKeySearchString = string.Empty;

    public Main(Plugin plugin) : base("Daily Routines - Main")
    {
        Flags = ImGuiWindowFlags.NoScrollbar;

        var assembly = Assembly.GetExecutingAssembly();
        var moduleTypes = assembly.GetTypes()
                                  .Where(t => typeof(IDailyModule).IsAssignableFrom(t) && t.IsClass);

        foreach (ModuleCategories category in Enum.GetValues(typeof(ModuleCategories)))
            ModuleCategories[category] = new List<Type>();

        foreach (var type in moduleTypes)
            CheckAndCache(type);

        return;

        static void CheckAndCache(Type type)
        {
            var attr = type.GetCustomAttribute<ModuleDescriptionAttribute>();
            if (attr == null) return;

            ModuleCategories[attr.Category].Add(type);
        }
    }

    public override void Draw()
    {
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##MainWindow-SearchInput", $"{Service.Lang.GetText("PleaseSearch")}...",
                                ref SearchString, 100);
        ImGui.Separator();

        if (ImGui.BeginTabBar("BasicTab"))
        {
            foreach (var module in ModuleCategories) DrawTabItemModules(module.Value, module.Key);

            DrawTabSettings();

            if (P.PluginInterface.IsDev)
            {
                if (ImGui.BeginTabItem("Dev"))
                {
                    if (ImGui.Button("获取点击名"))
                    {
                        foreach (var clickName in Click.GetClickNames())
                            Service.Log.Debug(clickName);
                    }

                    if (ImGui.Button("显示通知"))
                    {
                        if (DalamudReflector.TryGetDalamudPlugin("NotificationMaster", out var instance, true, true))
                        {
                            Safe(delegate
                            {
                                instance.GetType().Assembly.GetType("NotificationMaster.TrayIconManager", true).GetMethod("ShowToast").Invoke(null,
                                    ["测试通知", P.Name]);
                            }, true);
                        }
                    }

                    unsafe
                    {
                        if (ImGui.Button("传送到FLAG地图"))
                        {
                            var territoryId = AgentMap.Instance()->FlagMapMarker.TerritoryId;
                            if (Service.ClientState.TerritoryType != territoryId)
                            {
                                var aetheryte = territoryId == 399
                                                    ? Service.Data.GetExcelSheet<Map>().GetRow(territoryId)
                                                             ?.TerritoryType?.Value?.Aetheryte.Value
                                                    : Service.Data.GetExcelSheet<Aetheryte>()
                                                             .FirstOrDefault(
                                                                 x => x.IsAetheryte && x.Territory.Row == territoryId);

                                if (aetheryte != null) Telepo.Instance()->Teleport(aetheryte.RowId, 0);
                            }
                        }

                        if (ImGui.Button("传送到FLAG"))
                        {
                            var targetPos = new Vector3(AgentMap.Instance()->FlagMapMarker.XFloat, 0,
                                                        AgentMap.Instance()->FlagMapMarker.YFloat);
                            Teleport(targetPos);
                        }

                        if (ImGui.Button("Y + 5"))
                        {
                            var currentPos = Service.ClientState.LocalPlayer.Position;
                            Teleport(currentPos with { Y = currentPos.Y + 5 });
                        }

                        if (ImGui.Button("Y - 5"))
                        {
                            var currentPos = Service.ClientState.LocalPlayer.Position;
                            Teleport(currentPos with { Y = currentPos.Y - 5 });
                        }
                    }

                    ImGui.EndTabItem();
                }
            }

            ImGui.EndTabBar();
        }
    }

    private static bool? Teleport(Vector3 pos)
    {
        if (IsOccupied()) return false;

        if (Service.ClientState.LocalPlayer != null)
        {
            var address = Service.ClientState.LocalPlayer.Address;
            MemoryHelper.Write(address + 176, pos.X);
            MemoryHelper.Write(address + 180, pos.Y);
            MemoryHelper.Write(address + 184, pos.Z);

            return true;
        }

        return false;
    }

    private static void DrawTabItemModules(IReadOnlyList<Type> modules, ModuleCategories category)
    {
        if (ImGui.BeginTabItem(Service.Lang.GetText(category.ToString())))
        {
            for (var i = 0; i < modules.Count; i++)
            {
                ImGui.PushID($"{modules[i]}_{category}");
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

        var isWithUI = ModuleManager.Modules[module].WithConfigUI;
        var moduleChanged = ImGuiOm.CheckboxColored($"##{module.Name}", ref tempModuleBool);

        if (moduleChanged)
        {
            var enabled = Service.Config.ModuleEnabled[boolName] = !Service.Config.ModuleEnabled[boolName];
            var component = ModuleManager.Modules[module];
            if (enabled) ModuleManager.Load(component);
            else ModuleManager.Unload(component);

            Service.Config.Save();
        }

        var moduleText = $"[{module.Name}]";
        ImGui.SameLine();
        var origCursorPos = ImGui.GetCursorPosX();
        ImGui.SetCursorPosX(ImGui.GetWindowWidth() - ImGui.CalcTextSize(moduleText).X -
                            (2 * ImGui.GetStyle().FramePadding.X));
        ImGui.TextDisabled(moduleText);

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

        moduleInstance?.ConfigUI();
    }

    private static void DrawTabSettings()
    {
        if (ImGui.BeginTabItem(Service.Lang.GetText("Settings")))
        {
            // 第一列
            ImGui.BeginGroup();
            ImGuiOm.TextIcon(FontAwesomeIcon.Globe, $"{Service.Lang.GetText("Language")}:");

            ImGui.SameLine();
            ImGui.SetNextItemWidth(240f);
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
            ImGui.SetNextItemWidth(150f);
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

            ImGui.Separator();

            ImGui.TextColored(ImGuiColors.DalamudYellow, $"{Service.Lang.GetText("Contact")}:");

            if (ImGui.Button("GitHub")) Util.OpenLink("https://github.com/AtmoOmen/DailyRoutines");

            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.DalamudOrange, Service.Lang.GetText("ContactHelp"));

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

    public void Dispose()
    {
        Service.Config.Save();
    }
}
