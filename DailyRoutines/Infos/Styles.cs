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
        var isOpen = true;
        ImGui.SetWindowSize(Vector2.Zero);
        ImGui.SetNextWindowSizeConstraints(Vector2.Zero, Vector2.One);
        ImGui.SetNextWindowSize(Vector2.Zero);
        ImGui.SetNextWindowContentSize(Vector2.Zero);
        ImGui.SetNextWindowPos(new(25000f * ImGuiHelpers.GlobalScale));
        if (ImGui.Begin("###DailyRoutines-Styles", ref isOpen, ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoBringToFrontOnFocus))
        {
            ImGui.CloseCurrentPopup();
            
            var style = ImGui.GetStyle();

            var checkboxSizeBool = false;
            ImGui.Checkbox("", ref checkboxSizeBool);
            CheckboxSize = ImGui.GetItemRectSize() - style.FramePadding / 2;

            ImGui.RadioButton("", false);
            RadioButtionSize = ImGui.GetItemRectSize() - style.FramePadding / 2;
            ImGui.End();
        }
    }
}
