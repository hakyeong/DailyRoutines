using System.Collections.Generic;
using System.Runtime.InteropServices;
using ClickLib.Clicks;
using DailyRoutines.Helpers;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoPreserveCollectableTitle", "AutoPreserveCollectableDescription", ModuleCategories.界面操作)]
public class AutoPreserveCollectable : DailyModuleBase
{
    private static readonly HashSet<uint> GatherJobs = [16, 17, 18];
    private static string PreserveMessage = string.Empty;

    public override void Init()
    {
        PreserveMessage = (LuminaCache.GetRow<Addon>(1463).Text.ToDalamudString().Payloads[0] as TextPayload).Text;

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "SelectYesno", OnAddon);
    }

    private static unsafe void OnAddon(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)args.Addon;
        if (addon == null) return;

        var localPlayer = Service.ClientState.LocalPlayer;
        if (localPlayer == null || !GatherJobs.Contains(localPlayer.ClassJob.Id)) return;

        var title = Marshal.PtrToStringUTF8((nint)addon->AtkValues[0].String);
        if (string.IsNullOrWhiteSpace(title) || !title.Contains(PreserveMessage)) return;

        ClickSelectYesNo.Using(args.Addon).Yes();
    }

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddon);
    }
}
