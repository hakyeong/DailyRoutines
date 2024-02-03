using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.AddonLifecycle;
using Dalamud.Interface.Internal.Notifications;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ClickLib.Clicks;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoQuestCompleteTitle", "AutoQuestCompleteDescription", ModuleCategories.Base)]
public class AutoQuestComplete : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithConfigUI => true;

    public void Init()
    {
        // 如果不用选, PostSetup 可以最快的完成点击, 要选就用 PostDraw 处理
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "JournalResult", OnAddonSetup);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, "JournalResult", OnAddonSetup);
    }

    public void ConfigUI()
    {
        ImGui.Text($"{Service.Lang.GetText("ConflictKey")}: {Service.Config.ConflictKey}");
    }

    public void OverlayUI() { }

    private unsafe void OnAddonSetup(AddonEvent type, AddonArgs args)
    {
        if (Service.KeyState[Service.Config.ConflictKey])
        {
            P.PluginInterface.UiBuilder.AddNotification(Service.Lang.GetText("ConflictKey-InterruptMessage"),
                                                        "Daily Routines", NotificationType.Success);
            return;
        }

        var addon = (AtkUnitBase*)args.Addon;
        if (addon == null) return;

        var handler = new ClickJournalResult();
        handler.Complete();
    }

    public void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddonSetup);
    }
}
