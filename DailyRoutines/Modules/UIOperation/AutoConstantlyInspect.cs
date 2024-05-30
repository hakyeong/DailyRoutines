using DailyRoutines.Helpers;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Interface.Internal.Notifications;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoConstantlyInspectTitle", "AutoConstantlyInspectDescription", ModuleCategories.界面操作)]
public class AutoConstantlyInspect : DailyModuleBase
{
    public override void Init()
    {
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "ItemInspectionResult", OnAddon);
    }

    public override void ConfigUI()
    {
        ConflictKeyText();
    }

    private static unsafe void OnAddon(AddonEvent type, AddonArgs args)
    {
        if (Service.KeyState[Service.Config.ConflictKey])
        {
            NotifyHelper.NotificationSuccess(Service.Lang.GetText("ConflictKey-InterruptMessage"));
            return;
        }

        var addon = (AtkUnitBase*)args.Addon;
        if (addon == null) return;

        var nextButton = addon->GetButtonNodeById(74);
        if (nextButton == null || !nextButton->IsEnabled) return;
        AgentHelper.SendEvent(AgentId.ItemInspection, 3, 0);
        addon->Close(true);
    }

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddon);

        base.Uninit();
    }
}
