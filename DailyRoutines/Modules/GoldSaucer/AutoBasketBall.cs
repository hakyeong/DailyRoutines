using ClickLib;
using DailyRoutines.Clicks;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Interface.Internal.Notifications;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using GameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoMTTitle", "AutoMTDescription", ModuleCategories.GoldSaucer)]
public class AutoBasketBall : DailyModuleBase
{
    public override void Init()
    {
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 10000, ShowDebug = false };

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, "BasketBall", OnAddonSetup);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "BasketBall", OnAddonSetup);

    }

    public override void ConfigUI()
    {
        ConflictKeyText();
        ImGuiOm.HelpMarker(Service.Lang.GetText("AutoMT-InterruptNotice"));
    }

    private unsafe void OnAddonSetup(AddonEvent type, AddonArgs args)
    {
        switch (type)
        {
            case AddonEvent.PostDraw:
                if (InterruptByConflictKey()) return;

                if (TryGetAddonByName<AtkUnitBase>("BasketBall", out var addon) && IsAddonReady(addon))
                {
                    if (TryGetAddonByName<AddonSelectString>("SelectString", out var addonSelectString) &&
                        IsAddonReady(&addonSelectString->AtkUnitBase))
                    {
                        Click.TrySendClick("select_string1");
                        return;
                    }

                    var button = addon->GetButtonNodeById(10);
                    if (button == null || !button->IsEnabled) return;

                    // 让进度条时时刻刻都是满的
                    addon->GetNodeById(12)->ChildNode->PrevSiblingNode->PrevSiblingNode->SetWidth(450);

                    var handler = new ClickBasketBallDR();
                    handler.Play(true);
                }

                break;
            case AddonEvent.PreFinalize:
                if (InterruptByConflictKey()) return;

                TaskManager.Enqueue(StartAnotherRound);
                break;
        }
    }

    private unsafe bool? StartAnotherRound()
    {
        if (InterruptByConflictKey()) return true;

        if (IsOccupied()) return false;
        var machineTarget = Service.Target.PreviousTarget;
        var machine = machineTarget.Name.ExtractText().Contains("怪物投篮") ? (GameObject*)machineTarget.Address : null;

        if (machine != null)
        {
            TargetSystem.Instance()->InteractWithObject(machine);
            return true;
        }

        return false;
    }

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddonSetup);
        Service.AddonLifecycle.UnregisterListener(OnAddonSetup);

        base.Uninit();
    }
}
