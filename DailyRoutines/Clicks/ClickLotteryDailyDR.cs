using System.Collections.Generic;
using ClickLib.Bases;
using ClickLib.Enums;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace DailyRoutines.Clicks;

public class ClickLotteryDailyDR(nint addon = default) : ClickBase<ClickLotteryDailyDR, AddonLotteryDaily>("LotteryDaily", addon)
{
    public void Block(uint index) => FireCallback(1, index);

    public unsafe void LineByBlocks(AtkComponentCheckBox* button0, AtkComponentCheckBox* button1, AtkComponentCheckBox* button2)
    {
        ClickAddonCheckBox(button0, 5);
        ClickAddonCheckBox(button1, 5);
        ClickAddonCheckBox(button2, 5);
    }

    public void Confirm() => FireCallback(2, 0);

    public void Exit() => FireCallback(-1);
}
