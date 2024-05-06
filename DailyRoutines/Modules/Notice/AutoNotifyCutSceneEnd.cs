using System;
using System.Diagnostics;
using System.Linq;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using DailyRoutines.Notifications;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoNotifyCutSceneEndTitle", "AutoNotifyCutSceneEndDescription", ModuleCategories.通知)]
public class AutoNotifyCutSceneEnd : DailyModuleBase
{
    private static bool OnlyNotifyWhenBackground;

    private static bool IsDutyEnd;
    private static bool IsSomeoneInCutscene;

    private static Stopwatch? Stopwatch;

    public override void Init()
    {
        Stopwatch ??= new Stopwatch();

        AddConfig("OnlyNotifyWhenBackground", true);
        OnlyNotifyWhenBackground = GetConfig<bool>("OnlyNotifyWhenBackground");

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "_PartyList", OnPartyList);
        Service.FrameworkManager.Register(OnUpdate);
        Service.DutyState.DutyCompleted += OnDutyComplete;
        Service.ClientState.TerritoryChanged += OnZoneChanged;
    }

    public override void ConfigUI()
    {
        PreviewImageWithHelpText(Service.Lang.GetText("AutoNotifyCutSceneEnd-NotificationMessageHelp"),
                                 "https://gh.atmoomen.top/DailyRoutines/main/Assets/Images/AutoNotifyCutSceneEnd-1.png");

        if (ImGui.Checkbox(Service.Lang.GetText("OnlyNotifyWhenBackground"),
                           ref OnlyNotifyWhenBackground))
            UpdateConfig("OnlyNotifyWhenBackground", OnlyNotifyWhenBackground);
    }

    private static unsafe void OnPartyList(AddonEvent type, AddonArgs args)
    {
        if (IsSomeoneInCutscene || IsDutyEnd || Service.ClientState.IsPvP || !Flags.BoundByDuty()) return;

        var isSBInCutScene = Service.PartyList.Any(member => member.GameObject != null &&
                                                             ((Character*)member.GameObject.Address)->CharacterData
                                                             .OnlineStatus == 15);

        if (isSBInCutScene)
        {
            Stopwatch.Restart();
            IsSomeoneInCutscene = true;
        }
    }

    private static unsafe void OnUpdate(IFramework framework)
    {
        if (!IsSomeoneInCutscene) return;
        if (!EzThrottler.Throttle("AutoNotifyCutSceneEnd")) return;

        var isSBInCutScene = Service.PartyList.Any(member => member.GameObject != null &&
                                                             ((Character*)member.GameObject.Address)->CharacterData
                                                             .OnlineStatus == 15);
        if (isSBInCutScene) return;

        IsSomeoneInCutscene = false;

        if (Stopwatch.Elapsed < TimeSpan.FromSeconds(4))
            Stopwatch.Reset();
        else
        {
            if (!OnlyNotifyWhenBackground || (OnlyNotifyWhenBackground && Framework.Instance()->WindowInactive))
                WinToast.Notify("", Service.Lang.GetText("AutoNotifyCutSceneEnd-NotificationMessage"));
        }
    }

    private static void OnZoneChanged(ushort zone)
    {
        Stopwatch.Reset();
        IsDutyEnd = false;
    }

    private static void OnDutyComplete(object? sender, ushort duty)
    {
        Stopwatch.Reset();
        IsDutyEnd = true;
    }

    public override void Uninit()
    {
        Stopwatch.Reset();
        Stopwatch = null;

        Service.DutyState.DutyCompleted -= OnDutyComplete;
        Service.ClientState.TerritoryChanged -= OnZoneChanged;
        Service.AddonLifecycle.UnregisterListener(OnPartyList);

        base.Uninit();
    }
}
