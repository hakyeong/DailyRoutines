using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoCommenceDutyTitle", "AutoCommenceDutyDescription", ModuleCategories.Base)]
public class AutoCommenceDuty : DailyModuleBase
{
    public override string? Author { get; set; } = "Cindy-Master";

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
