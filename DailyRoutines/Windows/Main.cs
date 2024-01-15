using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DailyRoutines.Infos;
using DailyRoutines.Manager;
using DailyRoutines.Managers;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using OmenTools.ImGuiOm;

namespace DailyRoutines.Windows;

public class Main : Window, IDisposable
{
    private static readonly List<Type> GeneralModules = new();
    private static readonly List<Type> GoldSaucerModules = new();

    public Main(Plugin plugin) : base(
        "Daily Routines - Main",
        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
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
                    if (i < GeneralModules.Count - 1) ImGui.Separator();
                }
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem(Service.Lang.GetText("GoldSaucer")))
            {
                for (var i = 0; i < GoldSaucerModules.Count; i++)
                {
                    DrawModuleCheckbox(GoldSaucerModules[i]);
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
                        if (ImGui.Selectable(languageInfo.DisplayName, Service.Config.SelectedLanguage == languageInfo.Language))
                        {
                            LanguageSwitchHandler(languageInfo.Language);
                        }
                        ImGuiOm.TooltipHover($"By: {string.Join(", ", languageInfo.Translators)}");

                        if (i + 1 != LanguageManager.LanguageNames.Length) ImGui.Separator();
                    }
                    ImGui.EndCombo();
                }
                ImGui.EndTabItem();
            }


            ImGui.EndTabBar();
        }
    }

    private void DrawModuleCheckbox(Type module)
    {
        var boolName = module.Name;

        if (!Service.Config.ModuleEnabled.TryGetValue(boolName, out var cbool)) return;
        if (!typeof(IDailyModule).IsAssignableFrom(module)) return;

        var attributes = module.GetCustomAttributes(typeof(ModuleDescriptionAttribute), false);
        if (attributes.Length <= 0) return;

        var content = (ModuleDescriptionAttribute)attributes[0];
        var title = Service.Lang.GetText(content.TitleKey);
        var description = Service.Lang.GetText(content.DescriptionKey);

        if (ImGuiOm.CheckboxColored($"{title}##{module.Name}", ref cbool))
        {
            Service.Config.ModuleEnabled[boolName] = !Service.Config.ModuleEnabled[boolName];
            var component = ModuleManager.Modules.FirstOrDefault(c => c.GetType() == module);
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

    internal void LanguageSwitchHandler(string languageName)
    {
        Service.Config.SelectedLanguage = languageName;
        Service.Lang = new LanguageManager(Service.Config.SelectedLanguage);
        Service.Config.Save();

        P.CommandHandler();
    }

    public void Dispose() { }
}
