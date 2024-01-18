using ClickLib.Bases;
using FFXIVClientStructs.FFXIV.Client.UI;
using System.Collections.Generic;

namespace DailyRoutines.Clicks;

public class ClickGuildLeveDR(nint addon = default) : ClickBase<ClickGuildLeveDR>("GuildLeve", addon)
{
    public static readonly Dictionary<uint, int> JobCategoryIndex = new()
    {
        { 9, 0 },  // 刻木匠
        { 10, 1 }, // 锻铁匠
        { 11, 2 }, // 铸甲匠
        { 12, 3 }, // 雕金匠
        { 13, 4 }, // 制革匠
        { 14, 5 }, // 裁衣匠
        { 15, 6 }, // 炼金术师
        { 16, 7 }, // 烹调师
        { 17, 0 }, // 采矿工
        { 18, 1 }, // 园艺工
        { 19, 2 }  // 捕鱼人
    };

    public bool? LeveQuest(int index, int leveQuestId)
    {
        FireCallback(13, index, leveQuestId);

        return true;
    }

    public unsafe bool? SwitchJob(uint jobCategory)
    {
        if (TryGetAddonByName<AddonGuildLeve>("GuildLeve", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(&addon->AtkUnitBase))
        {
            if (!JobCategoryIndex.TryGetValue(jobCategory, out var index)) return false;
            FireCallback(12, index);
            return true;
        }

        return false;
    }

    public bool Exit()
    {
        FireCallback(-2);
        FireCallback(-1);

        return true;
    }
}

