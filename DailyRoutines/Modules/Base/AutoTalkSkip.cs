using ClickLib;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.AddonLifecycle;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoTalkSkipTitle", "AutoTalkSkipDescription", ModuleCategories.Base)]
public class AutoTalkSkip : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithConfigUI => true;

    public void Init()
    {
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, "Talk", OnAddonDraw);
    }

    public void ConfigUI()
    {
        ImGui.Text($"{Service.Lang.GetText("ConflictKey")}: {Service.Config.ConflictKey}");
    }

    public void OverlayUI() { }

    private static void OnAddonDraw(AddonEvent type, AddonArgs args)
    {
        if (Service.KeyState[Service.Config.ConflictKey]) return;
        Click.SendClick("talk");
    }

    public void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddonDraw);
    }
}
