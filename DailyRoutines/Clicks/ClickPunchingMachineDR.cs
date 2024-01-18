using ClickLib.Bases;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;

namespace DailyRoutines.Clicks;

public class ClickPunchingMachineDR(nint addon = default) : ClickBase<ClickPunchingMachineDR>("PunchingMachine", addon)
{
    public unsafe bool? Button()
    {
        var ui = (AtkUnitBase*)AddonAddress;

        var button = ui->GetButtonNodeById(23);
        if (button == null || !button->IsEnabled) return false;

        FireCallback(11, 3, new Random().Next(1700, 1999));

        return true;
    }
}
