using ClickLib;
using DailyRoutines.Clicks;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.AddonLifecycle;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using GameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoMTTitle", "AutoMTDescription", ModuleCategories.GoldSaucer)]
public class AutoBasketBall : IDailyModule
{
    public bool Initialized { get; set; }

    private static TaskManager TaskManager = null!;

    public void UI() { }

    public void Init()
    {
        TaskManager = new TaskManager { AbortOnTimeout = true, TimeLimitMS = 10000, ShowDebug = false };

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, "BasketBall", OnAddonSetup);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "BasketBall", OnAddonSetup);

        Initialized = true;
    }

    private static unsafe void OnAddonSetup(AddonEvent type, AddonArgs args)
    {
        switch (type)
        {
            case AddonEvent.PostDraw:
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
                TaskManager.Enqueue(StartAnotherRound);
                break;
        }
    }

    private static unsafe bool? StartAnotherRound()
    {
        if (IsOccupied()) return false;
        var machineTarget = Service.Target.PreviousTarget;
        var machine = machineTarget.DataId == 2004804 ? (GameObject*)machineTarget.Address : null;

        if (machine != null)
        {
            TargetSystem.Instance()->InteractWithObject(machine);
            return true;
        }

        return false;
    }

    public void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddonSetup);
        Service.AddonLifecycle.UnregisterListener(OnAddonSetup);
        TaskManager?.Abort();

        Initialized = false;
    }
}
