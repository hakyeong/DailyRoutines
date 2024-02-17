using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.AddonLifecycle;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoNotifyDutyConfirmTitle", "AutoNotifyDutyConfirmDescription", ModuleCategories.Notice)]
public class AutoNotifyDutyConfirm : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithConfigUI => false;

    public void Init()
    {
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "ContentsFinderConfirm", OnAddonSetup);
    }

    public void ConfigUI() { }

    public void OverlayUI() { }

    private unsafe void OnAddonSetup(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)args.Addon;
        if (addon == null) return;

        var dutyName = MemoryHelper.ReadStringNullTerminated((nint)addon->AtkValues[1].String);
        if (string.IsNullOrWhiteSpace(dutyName)) return;

        var loc = Service.Lang.GetText("AutoNotifyDutyConfirm-NoticeMessage", dutyName);
        Service.Notification.Show(loc, loc);
    }

    public void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddonSetup);
    }
}
