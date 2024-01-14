namespace DailyRoutines.Windows;

public class Main : Window, IDisposable
{
    public Main(Plugin plugin) : base(
        "Main Window",
        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        Size = new Vector2(232, 75);
        SizeCondition = ImGuiCond.Once;
    }

    public override void Draw()
    {

    }
    public void Dispose() { }
}
