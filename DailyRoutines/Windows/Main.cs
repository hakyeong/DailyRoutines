namespace DailyRoutines.Windows;

public class Main : Window, IDisposable
{
    private static readonly ConcurrentBag<Type> GeneralModules = new();

    public Main(Plugin plugin) : base(
        "Main Window",
        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        Size = new Vector2(232, 75);
        SizeCondition = ImGuiCond.Once;

        var assembly = Assembly.GetExecutingAssembly();
        var typesWithInterface = assembly.GetTypes()
                                         .Where(t => typeof(IDailyModule).IsAssignableFrom(t) && t.IsClass);

        Parallel.ForEach(typesWithInterface, (type) =>
        {
            CheckAndCache(type);
        });

        return;
        
        static void CheckAndCache(Type type)
        {
            var attr = type.GetCustomAttribute<ModuleDescriptionAttribute>();
            if (attr == null) return;

            switch (attr.Category)
            {
                case "General": 
                    GeneralModules.Add(type);
                    break;
                default:
                    break;
            }
        }

    }

    public override void Draw()
    {
        if (ImGui.BeginTabBar("BasicTab"))
        {
            if (ImGui.BeginTabItem("General"))
            {
                foreach (var module in GeneralModules)
                {
                    DrawModuleCheckbox(typeof(module.key), )
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

        ImGui.TextDisabled(description);
    }


    public void Dispose() { }
}
