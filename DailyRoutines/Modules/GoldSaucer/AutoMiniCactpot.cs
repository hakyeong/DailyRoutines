using DailyRoutines.Clicks;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.AddonLifecycle;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoMiniCactpotTitle", "AutoMiniCactpotDescription", ModuleCategories.GoldSaucer)]
public class AutoMiniCactpot : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithConfigUI => false;

    private static TaskManager? TaskManager;

    public void ConfigUI() { }

    public void OverlayUI() { }

    public void Init()
    {
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = false };
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "LotteryDaily", OnAddonSetup);

        Initialized = true;
    }

    private static void OnAddonSetup(AddonEvent type, AddonArgs args)
    {
        if (TaskManager.IsBusy) return;

        TaskManager.Enqueue(RandomClick);
        TaskManager.Enqueue(ClickExit);
    }

    private static unsafe bool? RandomClick()
    {
        if (TryGetAddonByName<AddonLotteryDaily>("LotteryDaily", out var addon) && IsAddonReady(&addon->AtkUnitBase))
        {
            var ui = &addon->AtkUnitBase;
            ui->GetButtonNodeById(67)->AtkComponentBase.SetEnabledState(true);

            if (!ui->GetButtonNodeById(67)->IsEnabled) return false;

            var clickHandler = new ClickLotteryDailyDR();
            clickHandler.Confirm();

            return true;
        }

        return false;
    }

    private static unsafe bool? ClickExit()
    {
        if (TryGetAddonByName<AddonLotteryDaily>("LotteryDaily", out var addon) && IsAddonReady(&addon->AtkUnitBase))
        {
            var ui = &addon->AtkUnitBase;
            var clickHandler = new ClickLotteryDailyDR();

            clickHandler.Exit();
            addon->AtkUnitBase.Close(true);

            return true;
        }

        return false;
    }

    public void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddonSetup);
        TaskManager?.Abort();

        Initialized = false;
    }
}
