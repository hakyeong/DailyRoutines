using System.Windows.Forms;
using DailyRoutines.Helpers;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoQTETitle", "AutoQTEDescription", ModuleCategories.战斗)]
public class AutoQTE : DailyModuleBase
{
    private static readonly string[] QTETypes = ["_QTEKeep", "_QTEMash", "_QTEKeepTime", "_QTEButton"];

    public override void Init()
    {
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, QTETypes, OnQTEAddon);
    }

    private static void OnQTEAddon(AddonEvent type, AddonArgs args)
    {
        WindowHelper.SendKeypress(Keys.Space);
    }

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnQTEAddon);
    }
}
