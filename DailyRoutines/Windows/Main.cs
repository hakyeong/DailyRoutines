using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ClickLib;
using DailyRoutines.Infos;
using DailyRoutines.Manager;
using DailyRoutines.Managers;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace DailyRoutines.Windows;

public class Main : Window, IDisposable
{
    private static readonly ConcurrentDictionary<Type, (string Name, string Title, string Description)> ModuleCache =
        new();

    private static readonly Dictionary<ModuleCategories, List<Type>> ModuleCategories = new();

    private static string SearchString = string.Empty;

    public Main(Plugin plugin) : base("Daily Routines - Main")
    {
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
        ImGui.InputTextWithHint("##MainWindow-SearchInput", $"{Service.Lang.GetText("PleaseSearch")}...", ref SearchString, 100);
        ImGui.Separator();

        if (ImGui.BeginTabBar("BasicTab"))
        {
            foreach (var module in ModuleCategories) DrawTabItemModules(module.Value, module.Key);

            if (ImGui.BeginTabItem(Service.Lang.GetText("Settings")))
            {
                ImGuiOm.TextIcon(FontAwesomeIcon.Globe, $"{Service.Lang.GetText("Language")}:");

                ImGui.SameLine();
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

                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Dev"))
            {
                if (ImGui.Button("获取测试点击"))
                {
                    foreach (var clickName in Click.GetClickNames())
                        Service.Log.Debug(clickName);
                }

                unsafe
                {
                    if (ImGui.Button("测试点击"))
                    {
                        var addon = (AtkUnitBase*)Service.Gui.GetAddonByName("Hummer");
                        if (addon != null) Callback.Fire(addon, true, 11, 4, 0);
                    }
                }

                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
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
            var title = string.Empty;
            var description = string.Empty;
            if (attributes.Length > 0)
            {
                var content = (ModuleDescriptionAttribute)attributes[0];
                title = Service.Lang.GetText(content.TitleKey);
                description = Service.Lang.GetText(content.DescriptionKey);
            }

            return (m.Name, title, description);
        });

        if (!Service.Config.ModuleEnabled.TryGetValue(boolName, out var tempModuleBool)) return;
        if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(description)) return;

        if (!string.IsNullOrEmpty(SearchString))
            if (!title.Contains(SearchString) && !description.Contains(SearchString))
                return;

        if (ImGuiOm.CheckboxColored($"{title}##{module.Name}", ref tempModuleBool))
        {
            Service.Config.ModuleEnabled[boolName] = !Service.Config.ModuleEnabled[boolName];
            var component = ModuleManager.Modules[module];
            if (Service.Config.ModuleEnabled[boolName])
                ModuleManager.Load(component);
            else
                ModuleManager.Unload(component);

            Service.Config.Save();
        }

        ImGuiOm.TextDisabledWrapped(description);

        if (tempModuleBool) DrawModuleUI(module);

        if (index < modulesCount - 1) ImGui.Separator();
    }

    private static void DrawModuleUI(Type module)
    {
        var moduleInstance = ModuleManager.Modules[module];

        moduleInstance?.UI();
    }

    internal void LanguageSwitchHandler(string languageName)
    {
        Service.Config.SelectedLanguage = languageName;
        Service.Lang = new LanguageManager(Service.Config.SelectedLanguage);
        Service.Config.Save();

        ModuleCache.Clear();
        P.CommandHandler();
    }

    public void Dispose() { }
}
