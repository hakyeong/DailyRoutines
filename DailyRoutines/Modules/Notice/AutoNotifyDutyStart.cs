using DailyRoutines.Infos;
using DailyRoutines.Managers;
using DailyRoutines.Notifications;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoNotifyDutyStartTitle", "AutoNotifyDutyStartDescription", ModuleCategories.通知)]
public class AutoNotifyDutyStart : DailyModuleBase
{
    private static bool ConfigOnlyNotifyWhenBackground;

    public override void Init()
    {
        AddConfig("OnlyNotifyWhenBackground", true);
        ConfigOnlyNotifyWhenBackground = GetConfig<bool>("OnlyNotifyWhenBackground");

        Service.DutyState.DutyStarted += OnDutyStart;
    }

    public override void ConfigUI()
    {
        if (ImGui.Checkbox(Service.Lang.GetText("OnlyNotifyWhenBackground"), ref ConfigOnlyNotifyWhenBackground))
            UpdateConfig("OnlyNotifyWhenBackground", ConfigOnlyNotifyWhenBackground);
    }

    private static void OnDutyStart(object? sender, ushort e)
    {
        if (!ConfigOnlyNotifyWhenBackground || (ConfigOnlyNotifyWhenBackground && !IsGameForeground()))
            WinToast.Notify("", Service.Lang.GetText("AutoNotifyDutyStart-NotificationMessage"));
    }
}
