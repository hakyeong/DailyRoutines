using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Client.UI;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;


namespace DailyRoutines.Modules;



[ModuleDescription("AutoJoinDutyTitle", "AutoJoinDutyDescription", ModuleCategories.Base)]
public class AutoJoinDuty : DailyModuleBase
{
    public override string? Author { get; set; } = "Cindy-Master";

    public override void Init()
    {
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "ContentsFinderConfirm", OnAddonSetup);
    }
    

        private static unsafe void OnAddonSetup(AddonEvent type, AddonArgs args)
    {
        var addon = RaptureAtkUnitManager.Instance()->GetAddonByName("ContentsFinderConfirm");

        var eventData = new AtkEvent();
        var inputData = stackalloc int[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        addon->ReceiveEvent(AtkEventType.ButtonClick, 8, &eventData, (nint)inputData);
    }



    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddonSetup);

        base.Uninit();
    }
}

