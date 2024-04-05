using System.Windows.Forms;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using ECommons.Automation;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoQTETitle", "AutoQTEDescription", ModuleCategories.CombatExpand)]
public class AutoQTE : DailyModuleBase
{
    private static readonly string[] QTETypes = ["_QTEKeep", "_QTEMash", "_QTEKeepTime", "_QTEButton"];

    public override void Init()
    {
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, QTETypes, OnQTEAddon);
    }

    private static void OnQTEAddon(AddonEvent type, AddonArgs args)
    {
        WindowsKeypress.SendKeypress(Keys.Space);
    }

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnQTEAddon);

        base.Uninit();
    }
}
