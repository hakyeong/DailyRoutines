namespace DailyRoutines.Modules;

/*
[ModuleDescription("AutoMiniCactpotTitle", "AutoMiniCactpotDescription", ModuleCategories.GoldSaucer)]
public class AutoMiniCactpot : IDailyModule
{
    public bool Initialized { get; set; }

    // 从左上到右下 From Top Left to Bottom Right
    private static readonly Dictionary<uint, uint> BlockNodeIds = new()
    {
        { 30, 0 },
        { 31, 1 },
        { 32, 2 },
        { 33, 3 },
        { 34, 4 },
        { 35, 5 },
        { 36, 6 },
        { 37, 7 },
        { 38, 8 }
    };

    // 从左下到右上 From Bottom Left to Top Right
    private static readonly uint[] LineNodeIds = { 28, 27, 26, 21, 22, 23, 24, 25 };


    public void Init()
    {
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, "Talk", OnAddonTalk);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "LotteryDaily", OnAddonSetup);

        Initialized = true;
    }

    private void OnAddonTalk(AddonEvent type, AddonArgs args)
    {
        if (Service.ClientState.TerritoryType != 144) return;
        var broker = Service.Target.Target;
        if (broker == null || broker.DataId != 1010445) return;
        Click.SendClick("talk");
    }

    private unsafe void OnAddonSetup(AddonEvent type, AddonArgs args)
    {
        var ui = (AtkUnitBase*)args.Addon;
        if (!HelpersOm.IsAddonAndNodesReady(ui)) return;

        var stateCheck1 = TaskManager1.WaitForExpectedResult(IsOnDisabledLD, false, TimeSpan.FromSeconds(10)).GetAwaiter()
                                     .GetResult();
        if (!stateCheck1) return;

        var clickHandler = new ClickLotteryDaily((nint)ui);
        var rnd = new Random();

        var selectedBlocks = BlockNodeIds.Keys.OrderBy(x => rnd.Next()).Take(4).ToArray();
        var selectedLine = LineNodeIds.OrderBy(x => rnd.Next()).LastOrDefault();

        foreach (var id in selectedBlocks)
        {
            var blockButton = ui->GetComponentNodeById(id);
            if (blockButton == null) continue;

            clickHandler.ClickBlockButton(BlockNodeIds[id]);
        }

        var stateCheck2 = TaskManager1.WaitForExpectedResult(IsOnDisabledLD, false, TimeSpan.FromSeconds(10)).GetAwaiter()
                                     .GetResult();
        if (!stateCheck2) return;

        var radioButton = ui->GetComponentNodeById(selectedLine);
        if (radioButton == null) return;

        clickHandler.ClickLineButton((AtkComponentRadioButton*)radioButton);

        var stateCheck3 = TaskManager1.WaitForExpectedResult(IsOnDisabledLD, false, TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
        if (!stateCheck3) return;

        clickHandler.ClickConfirmButton();
        clickHandler.ClickExistButton();
    }

    private unsafe bool IsOnDisabledLD()
    {
        var ui = (AtkUnitBase*)Service.Gui.GetAddonByName("LotteryDaily");
        if (!HelpersOm.IsAddonAndNodesReady(ui)) return true;

        return ui->GetImageNodeById(4)->AtkResNode.IsVisible || ui->GetTextNodeById(3)->AtkResNode.IsVisible ||
               ui->GetTextNodeById(2)->AtkResNode.IsVisible;
    }

    public void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddonTalk);
        Service.AddonLifecycle.UnregisterListener(OnAddonSetup);

        Initialized = false;
    }
}

public class ClickLotteryDaily(nint addon = default)
    : ClickBase<ClickLotteryDaily, AddonLotteryDaily>("LotteryDaily", addon)
{
    public void ClickBlockButton(uint index)
    {
        FireCallback(1, index);
    }

    public unsafe void ClickLineButton(AtkComponentRadioButton* button)
    {
        ClickAddonRadioButton(button, 8);
    }

    public void ClickConfirmButton()
    {
        FireCallback(2, 0);
    }

    public void ClickExistButton()
    {
        FireCallback(-1);
    }
}
*/
