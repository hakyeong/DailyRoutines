using ClickLib.Clicks;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoCommenceDutyTitle", "AutoCommenceDutyDescription", ModuleCategories.界面操作, "Cindy-Master")]
public class AutoCommenceDuty : DailyModuleBase
{
    public override void Init()
    {
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "ContentsFinderConfirm", OnAddonSetup);
    }

    private static unsafe void OnAddonSetup(AddonEvent type, AddonArgs args)
    {
        var addon = RaptureAtkUnitManager.Instance()->GetAddonByName("ContentsFinderConfirm");

        ClickContentsFinderConfirm.Using((nint)addon).Commence();
    }

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddonSetup);
        base.Uninit();
    }
}
