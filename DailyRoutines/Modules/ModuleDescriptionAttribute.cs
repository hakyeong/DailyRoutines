using System;

namespace DailyRoutines.Modules;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class ModuleDescriptionAttribute(string titleKey, string descriptionKey, ModuleCategories category, string? author = null) : Attribute
{
    public string           TitleKey       { get; } = titleKey;
    public string           DescriptionKey { get; } = descriptionKey;
    public ModuleCategories Category       { get; } = category;
    public string?          Author         { get; } = author;
}
