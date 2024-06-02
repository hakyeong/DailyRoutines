using DailyRoutines.Managers;
using DailyRoutines.Notifications;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoNotifyDutyConfirmTitle", "AutoNotifyDutyConfirmDescription", ModuleCategories.通知)]
public class AutoNotifyDutyConfirm : DailyModuleBase
{
    public override void Init()
    {
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "ContentsFinderConfirm", OnAddonSetup);
    }

    private static unsafe void OnAddonSetup(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)args.Addon;
        if (addon == null) return;

        var dutyName = MemoryHelper.ReadStringNullTerminated((nint)addon->AtkValues[1].String);
        if (string.IsNullOrWhiteSpace(dutyName)) return;

        var loc = Service.Lang.GetText("AutoNotifyDutyConfirm-NoticeMessage", dutyName);
        WinToast.Notify(loc, loc);
    }

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddonSetup);

        base.Uninit();
    }
}
