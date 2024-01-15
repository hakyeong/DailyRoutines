namespace DailyRoutines.Infos;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class ModuleDescriptionAttribute(string titleKey, string descriptionKey, ModuleCategories category) : Attribute
{
    public string TitleKey { get; } = titleKey;
    public string DescriptionKey { get; } = descriptionKey;
    public ModuleCategories Category { get; } = category;
}
