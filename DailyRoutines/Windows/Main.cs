using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ClickLib;
using DailyRoutines.Infos;
using DailyRoutines.Manager;
using DailyRoutines.Managers;
using DailyRoutines.Modules;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace DailyRoutines.Windows;

public class Main : Window, IDisposable
{
    private static readonly
        ConcurrentDictionary<Type, (string Name, string Title, string Description, IDailyModule ModuleInstance)>
        _moduleCache
            = new();

    private static readonly ConcurrentDictionary<Type, (string Name, IDailyModule ModuleInstance)> _moduleInstanceCache
        = new();

    private static readonly List<Type> GeneralModules = new();
    private static readonly List<Type> GoldSaucerModules = new();
    private static readonly List<Type> RetainerModules = new();

    public Main(Plugin plugin) : base("Daily Routines - Main")
    {
        var assembly = Assembly.GetExecutingAssembly();
        var moduleTypes = assembly.GetTypes()
                                  .Where(t => typeof(IDailyModule).IsAssignableFrom(t) && t.IsClass);

        foreach (var type in moduleTypes)
            CheckAndCache(type);

        return;

        static void CheckAndCache(Type type)
        {
            var attr = type.GetCustomAttribute<ModuleDescriptionAttribute>();
            if (attr == null) return;

            switch (attr.Category)
            {
                case ModuleCategories.General:
                    GeneralModules.Add(type);
                    break;
                case ModuleCategories.GoldSaucer:
                    GoldSaucerModules.Add(type);
                    break;
                case ModuleCategories.Retainer:
                    RetainerModules.Add(type);
                    break;
                default:
                    Service.Log.Error("Unknown Modules");
                    break;
            }
        }
    }

    public override void Draw()
    {
        if (ImGui.BeginTabBar("BasicTab"))
        {
            if (ImGui.BeginTabItem(Service.Lang.GetText("General")))
            {
                for (var i = 0; i < GeneralModules.Count; i++)
                {
                    DrawModuleCheckbox(GeneralModules[i]);
                    DrawModuleUI(GeneralModules[i]);
                    if (i < GeneralModules.Count - 1) ImGui.Separator();
                }

                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem(Service.Lang.GetText("Retainer")))
            {
                for (var i = 0; i < RetainerModules.Count; i++)
                {
                    DrawModuleCheckbox(RetainerModules[i]);
                    DrawModuleUI(RetainerModules[i]);
                    if (i < RetainerModules.Count - 1) ImGui.Separator();
                }

                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem(Service.Lang.GetText("GoldSaucer")))
            {
                for (var i = 0; i < GoldSaucerModules.Count; i++)
                {
                    DrawModuleCheckbox(GoldSaucerModules[i]);
                    DrawModuleUI(GoldSaucerModules[i]);
                    if (i < GoldSaucerModules.Count - 1) ImGui.Separator();
                }

                ImGui.EndTabItem();
            }

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
                    foreach (var clickName in Click.GetClickNames()) Service.Log.Debug(clickName);
                }

                if (ImGui.Button("获取测试文本1"))
                {
                    var state = AutoMiniCactpot.IsEzMiniCactpotInstalled();
                    Service.Log.Debug(state ? "装了" : "没装");
                }

                if (ImGui.Button("获取测试文本2"))
                {
                    Service.Log.Debug(Service.ExcelData.ItemNames.Count.ToString());
                }

                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    private static void DrawModuleCheckbox(Type module)
    {
        var (boolName, title, description, _) = _moduleCache.GetOrAdd(module, m =>
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

            return (m.Name, title, description, null)!;
        });

        if (!Service.Config.ModuleEnabled.TryGetValue(boolName, out var cbool)) return;
        if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(description)) return;

        if (ImGuiOm.CheckboxColored($"{title}##{module.Name}", ref cbool))
        {
            Service.Config.ModuleEnabled[boolName] = !Service.Config.ModuleEnabled[boolName];
            var component = _moduleCache[module].ModuleInstance ??
                            ModuleManager.Modules.FirstOrDefault(c => c.GetType() == module);
            if (component != null)
            {
                if (Service.Config.ModuleEnabled[boolName])
                    ModuleManager.Load(component);
                else
                    ModuleManager.Unload(component);
            }
            else
                Service.Log.Error($"Fail to fetch module {module.Name}");

            Service.Config.Save();
        }

        ImGuiOm.TextDisabledWrapped(description);
    }

    private static void DrawModuleUI(Type module)
    {
        var boolName = module.Name;
        if (!Service.Config.ModuleEnabled.TryGetValue(boolName, out var cbool) || !cbool) return;

        var moduleInstance = _moduleInstanceCache.GetOrAdd(module, m =>
        {
            var instance = Activator.CreateInstance(m) as IDailyModule;
            return (m.Name, instance)!;
        }).ModuleInstance;

        moduleInstance?.UI();
    }

    internal void LanguageSwitchHandler(string languageName)
    {
        Service.Config.SelectedLanguage = languageName;
        Service.Lang = new LanguageManager(Service.Config.SelectedLanguage);
        Service.Config.Save();

        _moduleInstanceCache.Clear();
        _moduleCache.Clear();
        P.CommandHandler();
    }

    public void Dispose() { }
}
