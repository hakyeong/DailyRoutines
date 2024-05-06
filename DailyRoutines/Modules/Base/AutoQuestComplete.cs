using ClickLib.Clicks;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Interface.Internal.Notifications;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoQuestCompleteTitle", "AutoQuestCompleteDescription", ModuleCategories.基础)]
public class AutoQuestComplete : DailyModuleBase
{
    public override void Init()
    {
        // 如果不用选, PostSetup 可以最快的完成点击, 要选就用 PostDraw 处理
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "JournalResult", OnAddonSetup);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, "JournalResult", OnAddonSetup);
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

        var handler = new ClickJournalResult();
        handler.Complete();
    }

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddonSetup);
        base.Uninit();
    }
}
