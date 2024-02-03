using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.AddonLifecycle;
using Dalamud.Interface.Internal.Notifications;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoQuestAcceptTitle", "AutoQuestAcceptDescription", ModuleCategories.Base)]
public class AutoQuestAccept : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithConfigUI => true;

    public void Init()
    {
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "JournalAccept", OnAddonSetup);
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

        var questID = addon->AtkValues[226].UInt;

        Callback.Fire(addon, true, 3, questID);
    }

    public void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddonSetup);
    }
}
