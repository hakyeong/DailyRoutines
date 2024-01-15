namespace DailyRoutines.Modules;

/*
[ModuleDescription("AutoRetainerCollectTitle", "AutoRetainerCollectDescription", ModuleCategories.General)]
public class AutoRetainerCollect : IDailyModule
{
    public bool Initialized { get; set; }

    public void Init()
    {
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "RetainerList", OnAddonListSetup);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, "Talk", SkipTalk);

        Initialized = true;
    }

    private void SkipTalk(AddonEvent eventType, AddonArgs addonInfo)
    {
        var bell = Service.Target.Target;
        if (bell == null || bell.DataId != 2000401) return;
        Click.SendClick("talk");
    }

    private void OnAddonListSetup(AddonEvent type, AddonArgs args)
    {
        unsafe
        {
            var ui = (AtkUnitBase*)args.Addon;
            if (!HelpersOm.IsAddonAndNodesReady(ui)) return;
        }

        RetainerHandler(1);
    }

    private bool RetainerHandler(int index)
    {
        var retainerHandler = new ClickRetainerList();
        retainerHandler.Retainer(index);

        var stateCheck1 = TaskManager1.WaitForExpectedResult(IsSSAvailable, true, TimeSpan.FromSeconds(5)).Result;
        if (!stateCheck1) return false;

        Click.SendClick("select_string13");

        return true;
    }

    private unsafe bool IsSSAvailable()
    {
        var ui = (AtkUnitBase*)Service.Gui.GetAddonByName("SelectString");
        return HelpersOm.IsAddonAndNodesReady(ui);
    }

    public void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddonListSetup);
        Service.AddonLifecycle.UnregisterListener(SkipTalk);

        Initialized = false;
    }
}
*/
