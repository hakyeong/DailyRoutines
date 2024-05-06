using DailyRoutines.Helpers;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoQuestAcceptTitle", "AutoQuestAcceptDescription", ModuleCategories.界面操作)]
public class AutoQuestAccept : DailyModuleBase
{
    public override void Init()
    {
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "JournalAccept", OnAddonSetup);
    }

    public override void ConfigUI()
    {
        ConflictKeyText();
    }

    private unsafe void OnAddonSetup(AddonEvent type, AddonArgs args)
    {
        InterruptByConflictKey();

        var addon = (AtkUnitBase*)args.Addon;
        if (addon == null) return;

        var questID = addon->AtkValues[226].UInt;
        if (questID == 0) return;

        AddonHelper.Callback(addon, true, 3, questID);
    }

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddonSetup);

        base.Uninit();
    }
}
