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

[ModuleDescription("AutoInDutySelectYesTitle", "AutoInDutySelectYesDescription", ModuleCategories.CombatExpand)]
public partial class AutoInDutySelectYes : DailyModuleBase
{
    private static readonly HashSet<string> SelectYesSet = ["发现了", "退出任务"];

    public override void Init()
    {
        var currentZone = Service.ClientState.TerritoryType;
        if (Service.PresetData.Contents.ContainsKey(currentZone)) OnZoneChanged(currentZone);
        Service.ClientState.TerritoryChanged += OnZoneChanged;
    }

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
        if (SelectYesSet.Any(text.Contains) || SelectYesRegex().IsMatch(text)) Click.SendClick("select_yes");
    }

    public override void Uninit()
    {
        Service.ClientState.TerritoryChanged -= OnZoneChanged;
        Service.AddonLifecycle.UnregisterListener(OnAddonSelectYesno);

        base.Uninit();
    }

    [GeneratedRegex("^要(?:(?!传送邀请|救助|无法战斗|即将返回|开始地点|小队).)*吗？$")]
    private static partial Regex SelectYesRegex();
}
