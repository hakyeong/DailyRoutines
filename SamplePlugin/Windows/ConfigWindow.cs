namespace SamplePlugin.Windows;

public class ConfigWindow : Window, IDisposable
{

    public ConfigWindow(Plugin plugin) : base(
        "Unique Configuration Window Title",
        ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
        ImGuiWindowFlags.NoScrollWithMouse)
    {
        Size = new Vector2(232, 75);
        SizeCondition = ImGuiCond.Always;
    }

    public void Dispose() { }

    public override void Draw() { }
}
