using System;
using System.Runtime.InteropServices;
using ClickLib.Clicks;
using DailyRoutines.Infos;
using DailyRoutines.Infos.Clicks;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Memory;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoConfirmMateriaWorkTitle", "AutoConfirmMateriaWorkDescription", ModuleCategories.界面操作)]
public unsafe class AutoConfirmMateriaWork : DailyModuleBase
{
    public override void Init()
    {
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "SelectYesno", OnAddonYesno);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "MateriaAttachDialog", OnAddonAttach);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "MateriaRetrieveDialog", OnAddonRetrive);
    }

    private static void OnAddonAttach(AddonEvent type, AddonArgs args)
    {
        MemoryHelper.Write(Service.Condition.Address + 7, true);

        bool isAdvanced;
        try
        {
            isAdvanced = AddonState.MateriaAttachDialog->AtkValues[51].String != null &&
                !string.IsNullOrWhiteSpace(
                    Marshal.PtrToStringUTF8((nint)AddonState.MateriaAttachDialog->AtkValues[51].String));
        }
        catch (Exception)
        {
            isAdvanced = false;
        }

        if (isAdvanced)
        {
            SetComponentButtonChecked(AddonState.MateriaAttachDialog->GetNodeById(39)->GetAsAtkComponentButton(), true);
            ClickMateriaAttachDialog.Using(args.Addon).Confirm();
        }
        else
        {
            ClickMateriaAttachDialog.Using(args.Addon).Confirm();
            MemoryHelper.Write(Service.Condition.Address + 7, false);
            Service.ExecuteCommandManager.ExecuteCommand(ExecuteCommandFlag.LeaveMateriaAttachState);
        }
    }

    private static void OnAddonRetrive(AddonEvent type, AddonArgs args)
    {
        ClickMateriaRetrieveDialog.Using(args.Addon).Begin();
    }

    private static void OnAddonYesno(AddonEvent type, AddonArgs args)
    {
        if (AddonState.MateriaAttachDialog == null) return;

        ClickSelectYesNo.Using(args.Addon).Yes();
        MemoryHelper.Write(Service.Condition.Address + 7, false);
        Service.ExecuteCommandManager.ExecuteCommand(ExecuteCommandFlag.LeaveMateriaAttachState);
    }

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddonAttach);
        Service.AddonLifecycle.UnregisterListener(OnAddonYesno);
        Service.AddonLifecycle.UnregisterListener(OnAddonRetrive);
    }
}
