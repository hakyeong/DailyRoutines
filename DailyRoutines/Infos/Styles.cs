using System.Numerics;
using ImGuiNET;

namespace DailyRoutines.Infos;

public static class Styles
{
    public static Vector2 CheckboxSize { get; set; }

    public static void Refresh()
    {
        var style = ImGui.GetStyle();

        var checkboxSizeBool = false;
        ImGui.Checkbox("", ref checkboxSizeBool);
        CheckboxSize = ImGui.GetItemRectSize() - style.FramePadding / 2;
    }
}
