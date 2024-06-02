using ClickLib.Clicks;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoConfirmDesynthesizeDialogTitle", "AutoConfirmDesynthesizeDialogDescription",
                   ModuleCategories.界面操作)]
public unsafe class AutoConfirmDesynthesizeDialog : DailyModuleBase
{
    public override void Init() { Service.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, "SalvageDialog", OnAddon); }

    private static void OnAddon(AddonEvent type, AddonArgs args)
    {
        var addon = (AddonSalvageDialog*)args.Addon;
        if (addon == null) return;

        var handler = new ClickSalvageDialog();
        handler.CheckBox();
        handler.Desynthesize();
    }

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddon);

        base.Uninit();
    }
}
