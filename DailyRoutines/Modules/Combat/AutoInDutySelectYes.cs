using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ClickLib;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoInDutySelectYesTitle", "AutoInDutySelectYesDescription", ModuleCategories.Combat)]
public partial class AutoInDutySelectYes : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithConfigUI => false;

    private static readonly HashSet<string> SelectYesSet = ["发现了", "退出任务"];

    public void Init()
    {
        var currentZone = Service.ClientState.TerritoryType;
        if (Service.PresetData.Contents.ContainsKey(currentZone)) OnZoneChanged(currentZone);
        Service.ClientState.TerritoryChanged += OnZoneChanged;
    }

    public void ConfigUI() { }

    public void OverlayUI() { }

    private static void OnZoneChanged(ushort zone)
    {
        if (Service.PresetData.Contents.ContainsKey(zone))
            Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "SelectYesno", OnAddonSelectYesno);
        else
            Service.AddonLifecycle.UnregisterListener(OnAddonSelectYesno);
    }

    private static unsafe void OnAddonSelectYesno(AddonEvent type, AddonArgs args)
    {
        var addon = (AddonSelectYesno*)args.Addon;

        var text = addon->PromptText->NodeText.ExtractText();
        if (SelectYesRegex().IsMatch(text) || SelectYesSet.Any(text.Contains)) Click.SendClick("select_yes");
    }

    public void Uninit()
    {
        Service.ClientState.TerritoryChanged -= OnZoneChanged;
        Service.AddonLifecycle.UnregisterListener(OnAddonSelectYesno);
    }

    [GeneratedRegex("^要(?:(?!传送邀请|救助).)*吗？$")]
    private static partial Regex SelectYesRegex();
}
