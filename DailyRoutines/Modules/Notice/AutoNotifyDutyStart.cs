using DailyRoutines.Managers;
using DailyRoutines.Notifications;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
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

    private static unsafe void OnDutyStart(object? sender, ushort e)
    {
        if (!ConfigOnlyNotifyWhenBackground || (ConfigOnlyNotifyWhenBackground && !Framework.Instance()->WindowInactive))
            WinToast.Notify("", Service.Lang.GetText("AutoNotifyDutyStart-NotificationMessage"));
    }

    public override void Uninit()
    {
        Service.DutyState.DutyStarted -= OnDutyStart;
        base.Uninit();
    }
}
