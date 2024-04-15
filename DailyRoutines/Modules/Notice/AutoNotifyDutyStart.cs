using DailyRoutines.Infos;
using DailyRoutines.Managers;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoNotifyDutyStartTitle", "AutoNotifyDutyStartDescription", ModuleCategories.Notice)]
public class AutoNotifyDutyStart : DailyModuleBase
{
    private static bool ConfigOnlyNotifyWhenBackground;

    public override void Init()
    {
        AddConfig(this, "OnlyNotifyWhenBackground", true);
        ConfigOnlyNotifyWhenBackground = GetConfig<bool>(this, "OnlyNotifyWhenBackground");

        Service.DutyState.DutyStarted += OnDutyStart;
    }

    public override void ConfigUI()
    {
        if (ImGui.Checkbox(Service.Lang.GetText("OnlyNotifyWhenBackground"), ref ConfigOnlyNotifyWhenBackground))
            UpdateConfig(this, "OnlyNotifyWhenBackground", ConfigOnlyNotifyWhenBackground);
    }

    private static void OnDutyStart(object? sender, ushort e)
    {
        if (!ConfigOnlyNotifyWhenBackground || (ConfigOnlyNotifyWhenBackground && !HelpersOm.IsGameForeground()))
            WinToast.Notify("", Service.Lang.GetText("AutoNotifyDutyStart-NotificationMessage"));
    }
}
