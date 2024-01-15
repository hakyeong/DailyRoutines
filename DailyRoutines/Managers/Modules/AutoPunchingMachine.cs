namespace DailyRoutines.Managers.Modules;

[ModuleDescription("AutoCACTitle", "AutoCACDescription", ModuleCategories.GoldSaucer)]
public class AutoPunchingMachine : IDailyModule
{
    public bool Initialized { get; set; }

    private bool isPlaying;


    public void Init()
    {
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, "PunchingMachine", OnAddonSetup);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "PunchingMachine", OnAddonFinalize);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "GoldSaucerReward", OnAddonGSR);

        Initialized = true;
    }

    private unsafe void OnAddonSetup(AddonEvent type, AddonArgs args)
    {
        var ui = (AtkUnitBase*)args.Addon;
        if (!HelpersOm.IsAddonAndNodesReady(ui)) return;

        ClickStartGame();

        ClickGameButton();
    }

    private unsafe void ClickStartGame()
    {
        var ui = (AtkUnitBase*)Service.Gui.GetAddonByName("SelectString");
        if (!HelpersOm.IsAddonAndNodesReady(ui)) return;

        var clickString = new ClickSelectString();
        clickString.SelectItem1();
    }

    private unsafe void ClickGameButton()
    {
        var ui = (AtkUnitBase*)Service.Gui.GetAddonByName("PunchingMachine");

        ui->IsVisible = false;

        var button = ui->GetButtonNodeById(23);
        if (button == null || !button->IsEnabled) return;

        if (!isPlaying)
        {
            isPlaying = true;
            var handler = new ClickPunchingMachine();
            handler.ClickButton();
        }
    }

    private void OnAddonFinalize(AddonEvent type, AddonArgs args)
    {
        isPlaying = false;
    }

    private unsafe void OnAddonGSR(AddonEvent type, AddonArgs args)
    {
        var ui = (AtkUnitBase*)args.Addon;
        if (!HelpersOm.IsAddonAndNodesReady(ui)) return;

        ui->IsVisible = false;
    }

    public void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, "PunchingMachine", OnAddonSetup);
        Service.AddonLifecycle.UnregisterListener(AddonEvent.PreFinalize, "PunchingMachine", OnAddonFinalize);
        Service.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, "GoldSaucerReward", OnAddonGSR);
        isPlaying = false;

        Initialized = false;
    }
}

public class ClickPunchingMachine(IntPtr addon = default) : ClickBase<ClickPunchingMachine>("PunchingMachine", addon)
{
    public void ClickButton()
    {
        FireCallback(11, 3, new Random().Next(1700, 1800));
    }
}
