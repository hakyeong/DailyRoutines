using DailyRoutines.Infos;
using DailyRoutines.Managers;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoNotifyDutyNameTitle", "AutoNotifyDutyNameDescription", ModuleCategories.Notice)]
public class AutoNotifyDutyName : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithConfigUI => true;

    private static bool ConfigSendWindowsToast = true;

    public void Init()
    {
        Service.Config.AddConfig(this, "SendWindowsToast", true);

        ConfigSendWindowsToast = Service.Config.GetConfig<bool>(this, "SendWindowsToast");

        Service.ClientState.TerritoryChanged += OnZoneChange;
    }

    public void ConfigUI()
    {
        if (ImGui.Checkbox(Service.Lang.GetText("AutoNotifyDutyName-SendWindowsToast"), ref ConfigSendWindowsToast))
            Service.Config.UpdateConfig(this, "SendWindowsToast", ConfigSendWindowsToast);
    }

    public void OverlayUI() { }

    private static void OnZoneChange(ushort territory)
    {
        if (!Service.PresetData.Contents.TryGetValue(territory, out var content)) return;
        var contentName = content.Name.RawString;
        Service.Chat.Print(Service.Lang.GetSeString("AutoNotifyDutyName-NoticeMessage", contentName));
        if (ConfigSendWindowsToast) Service.Notice.Show(contentName, contentName);
    }

    public void Uninit()
    {
        Service.ClientState.TerritoryChanged -= OnZoneChange;
    }
}
