using ClickLib.Clicks;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoCheckDesynthesizeDialogTitle", "AutoCheckDesynthesizeDialogDescription", ModuleCategories.Base)]
public unsafe class AutoCheckDesynthesizeDialog : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithConfigUI => false;

    public void Init()
    {
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "SalvageDialog", OnAddon);
    }

    public void ConfigUI() { }

    public void OverlayUI() { }

    private static void OnAddon(AddonEvent type, AddonArgs args)
    {
        var addon = (AddonSalvageDialog*)args.Addon;
        if (addon == null) return;

        if (!addon->CheckBox->IsChecked)
        {
            MemoryHelper.Write((nint)addon->CheckBox + 232, addon->CheckBox->AtkComponentButton.Flags | 0x40000);
            var handler = new ClickSalvageDialog();
            handler.CheckBox();
        }
    }

    public void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddon);
    }
}
