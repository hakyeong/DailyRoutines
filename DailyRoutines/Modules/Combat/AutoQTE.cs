using System.Windows.Forms;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using ECommons.Automation;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoQTETitle", "AutoQTEDescription", ModuleCategories.Combat)]
public class AutoQTE : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithConfigUI => false;

    private static readonly string[] QTETypes = ["_QTEKeep", "_QTEMash", "_QTEKeepTime", "_QTEButton"];

    public void Init()
    {
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, QTETypes, OnQTEAddon);
    }

    public void ConfigUI() { }

    public void OverlayUI() { }

    private static void OnQTEAddon(AddonEvent type, AddonArgs args)
    {
        WindowsKeypress.SendKeypress(Keys.Space);
    }

    public void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnQTEAddon);

        Initialized = false;
    }
}
