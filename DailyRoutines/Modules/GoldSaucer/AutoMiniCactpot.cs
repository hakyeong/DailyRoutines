using System.Collections.Generic;
using System.Linq;
using DailyRoutines.Clicks;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.AddonLifecycle;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoMiniCactpotTitle", "AutoMiniCactpotDescription", ModuleCategories.GoldSaucer)]
public class AutoMiniCactpot : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithConfigUI => false;

    private static TaskManager? TaskManager;

    // 从左上到右下 From Top Left to Bottom Right  (Node ID - Callback Index)
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

    private static readonly Dictionary<uint, List<uint>> LineToBlocks = new()
    {
        { 28, [36, 37, 38] }, // 左下水平线
        { 27, [33, 34, 35] }, // 中间水平线
        { 26, [30, 31, 32] }, // 左上水平线
        { 21, [30, 34, 38] }, // 左上对角线
        { 22, [30, 33, 36] }, // 左侧垂直线
        { 23, [31, 34, 37] }, // 中间垂直线
        { 24, [32, 35, 38] }, // 右侧垂直线
        { 25, [32, 34, 36] }  // 右上对角线
    };

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
        // 装了 ezMiniCactpot -> 取插件算出来的相对优解
        if (IsEzMiniCactpotInstalled())
        {
            TaskManager.Enqueue(WaitLotteryDailyAddon);
            TaskManager.Enqueue(ClickRecommendBlock);
            TaskManager.Enqueue(ClickRecommendBlock);
            TaskManager.Enqueue(ClickRecommendBlock);
            TaskManager.Enqueue(WaitLotteryDailyAddon);
            TaskManager.Enqueue(ClickRecommendLine);
            TaskManager.Enqueue(WaitLotteryDailyAddon);
            TaskManager.Enqueue(ClickExit);
        }
        else // 没装 -> 随机取
        {
            TaskManager.Enqueue(WaitLotteryDailyAddon);
            TaskManager.Enqueue(RandomClick);
            TaskManager.Enqueue(WaitLotteryDailyAddon);
            TaskManager.Enqueue(ClickExit);
        }
    }

    private static unsafe bool? RandomClick()
    {
        if (TryGetAddonByName<AddonLotteryDaily>("LotteryDaily", out var addon) && IsAddonReady(&addon->AtkUnitBase))
        {
            var ui = &addon->AtkUnitBase;
            ui->GetButtonNodeById(67)->AtkComponentBase.SetEnabledState(true);

            if (!ui->GetButtonNodeById(67)->IsEnabled) return false;

            var clickHandler = new ClickLotteryDailyDR((nint)ui);
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
            var clickHandler = new ClickLotteryDailyDR((nint)ui);
            clickHandler.Exit();

            return true;
        }

        return false;
    }

    private static unsafe bool? ClickRecommendBlock()
    {
        if (TryGetAddonByName<AddonLotteryDaily>("LotteryDaily", out var addon) && IsAddonReady(&addon->AtkUnitBase))
        {
            var ui = &addon->AtkUnitBase;
            var clickHandler = new ClickLotteryDailyDR((nint)ui);

            for (var i = 0; i < 9; i++)
            {
                var node = addon->GameBoard[i];
                var resNode = node->AtkComponentButton.AtkComponentBase.OwnerNode->AtkResNode;
                if (resNode is { MultiplyBlue: 0, MultiplyRed: 0, MultiplyGreen: 100 })
                {
                    clickHandler.Block(resNode.NodeID);
                    return true;
                }
            }
        }

        return false;
    }

    private static unsafe bool? ClickRecommendLine()
    {
        if (TryGetAddonByName<AddonLotteryDaily>("LotteryDaily", out var addon) && IsAddonReady(&addon->AtkUnitBase))
        {
            var ui = &addon->AtkUnitBase;
            var clickHandler = new ClickLotteryDailyDR((nint)ui);
            for (var i = 0; i < 8; i++)
            {
                var resNode = addon->LaneSelector[i]->AtkComponentBase.OwnerNode->AtkResNode;

                if (resNode is { MultiplyBlue: 0, MultiplyRed: 0, MultiplyGreen: 100 })
                {
                    var blocks = LineToBlocks[resNode.NodeID];
                    clickHandler.LineByBlocks((AtkComponentCheckBox*)ui->GetComponentNodeById(blocks[0]),
                                              (AtkComponentCheckBox*)ui->GetComponentNodeById(blocks[1]),
                                              (AtkComponentCheckBox*)ui->GetComponentNodeById(blocks[2]));
                    break;
                }
            }

            clickHandler.Confirm();
            return true;
        }

        return false;
    }

    internal static bool IsEzMiniCactpotInstalled()
    {
        return P.PluginInterface.InstalledPlugins.Any(plugin => plugin is { Name: "ezMiniCactpot", IsLoaded: true });
    }

    private static unsafe bool? WaitLotteryDailyAddon()
    {
        if (TryGetAddonByName<AddonLotteryDaily>("LotteryDaily", out var addon) && IsAddonReady(&addon->AtkUnitBase))
        {
            var ui = &addon->AtkUnitBase;
            return !ui->GetImageNodeById(4)->AtkResNode.IsVisible && !ui->GetTextNodeById(3)->AtkResNode.IsVisible &&
                   !ui->GetTextNodeById(2)->AtkResNode.IsVisible;
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
