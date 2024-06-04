using System.Numerics;
using Dalamud.Interface.Utility;
using ImGuiNET;

namespace DailyRoutines.Infos;

public static class Styles
{
    public static Vector2 CheckboxSize { get; set; }
    public static Vector2 RadioButtionSize { get; set; }

    public static void Refresh()
    {
        ImGui.SetWindowPos(new(25000f));
        var style = ImGui.GetStyle();

        var checkboxSizeBool = false;
        ImGui.Checkbox("", ref checkboxSizeBool);
        CheckboxSize = ImGui.GetItemRectSize() - style.FramePadding / 2;

        ImGui.RadioButton("", false);
        RadioButtionSize = ImGui.GetItemRectSize() - style.FramePadding / 2;
    }
}
