using ClickLib.Bases;
using FFXIVClientStructs.FFXIV.Client.UI;
using System.Collections.Generic;

namespace DailyRoutines.Infos.Clicks;

public class ClickLotteryDaily(nint addon = default) : ClickBase<ClickLotteryDaily, AddonLotteryDaily>("LotteryDaily", addon)
{
    private static readonly Dictionary<uint, uint> BlockIDToCallbackIndex = new()
    {
        { 30, 0 }, // (0, 0)
        { 31, 1 }, // (0, 1)
        { 32, 2 }, // (0, 2)
        { 33, 3 }, // (1, 0)
        { 34, 4 }, // (1, 1)
        { 35, 5 }, // (1, 2)
        { 36, 6 }, // (2, 0)
        { 37, 7 }, // (2, 1)
        { 38, 8 }  // (2, 2)
    };

    public static readonly Dictionary<uint, int> LineNodeIDToUnkNumber3D4 = new()
    {
        { 22, 1 }, // 第一列 (从左到右)
        { 23, 2 }, // 第二列
        { 24, 3 }, // 第二列
        { 26, 5 }, // 第一行 (从上到下) 
        { 27, 6 }, // 第二行
        { 28, 7 }, // 第二行
        { 21, 0 }, // 左侧对角线
        { 25, 4 }  // 右侧对角线
    };

    public void Block(uint nodeID)
    {
        if (!BlockIDToCallbackIndex.TryGetValue(nodeID, out var index)) return;
        FireCallback(1, index);
    }

    public unsafe int Line(uint nodeID)
    {
        if (AddonAddress == nint.Zero) return -1;
        var unkNumber3D4 = LineNodeIDToUnkNumber3D4[nodeID];
        ((AddonLotteryDaily*)AddonAddress)->UnkNumber3D4 = unkNumber3D4;

        return unkNumber3D4;
    }

    public void Confirm(int index) => FireCallback(2, index);

    public void Exit() => FireCallback(-1);

    public static ClickLotteryDaily Using(nint addon) => new(addon);
}
