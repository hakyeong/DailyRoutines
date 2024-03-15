using DailyRoutines.Infos;
using DailyRoutines.Managers;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoNotifyDutyNameTitle", "AutoNotifyDutyNameDescription", ModuleCategories.Notice)]
public class AutoNotifyDutyName : DailyModuleBase
{
    private static bool ConfigSendWindowsToast = true;

    public override void Init()
    {
        AddConfig(this, "SendWindowsToast", true);
        ConfigSendWindowsToast = GetConfig<bool>(this, "SendWindowsToast");

        Service.ClientState.TerritoryChanged += OnZoneChange;
    }

    public override void ConfigUI()
    {
        if (ImGui.Checkbox(Service.Lang.GetText("AutoNotifyDutyName-SendWindowsToast"), ref ConfigSendWindowsToast))
            UpdateConfig(this, "SendWindowsToast", ConfigSendWindowsToast);
    }

    private static void OnZoneChange(ushort territory)
    {
        if (!Service.PresetData.Contents.TryGetValue(territory, out var content)) return;
        var contentName = content.Name.RawString;
        Service.Chat.Print(Service.Lang.GetSeString("AutoNotifyDutyName-NoticeMessage", contentName));
        if (ConfigSendWindowsToast) Service.Notice.Notify(contentName, contentName);
    }

    public override void Uninit()
    {
        Service.ClientState.TerritoryChanged -= OnZoneChange;

        base.Uninit();
    }
}
