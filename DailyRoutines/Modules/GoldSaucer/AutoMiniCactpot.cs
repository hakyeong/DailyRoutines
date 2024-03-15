using System.Collections.Generic;
using System.Linq;
using ClickLib;
using DailyRoutines.Clicks;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoMiniCactpotTitle", "AutoMiniCactpotDescription", ModuleCategories.GoldSaucer)]
public unsafe class AutoMiniCactpot : DailyModuleBase
{
    // 从左上到右下
    private static readonly Dictionary<uint, uint> BlockToCallbackIndex = new()
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

    private static readonly Dictionary<uint, int> LineToUnkNumber3D4 = new()
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

    private static int SelectedLineNumber3D4;

    public override void Init()
    {
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = false };
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "LotteryDaily", OnAddonSetup);
    }

    private void OnAddonSetup(AddonEvent type, AddonArgs args)
    {
        if (IsEzMiniCactpotInstalled())
            TaskManager.Enqueue(ClickHighlightBlocks);
        else
            TaskManager.Enqueue(RandomClick);
    }

    private bool? RandomClick()
    {
        if (!WaitLotteryDailyAddon()) return false;
        if (TryGetAddonByName<AtkUnitBase>("LotteryDaily", out var addon) && IsAddonReady(addon))
        {
            addon->GetButtonNodeById(67)->AtkComponentBase.SetEnabledState(true);

            if (!addon->GetButtonNodeById(67)->IsEnabled) return false;

            var clickHandler = new ClickLotteryDailyDR();
            clickHandler.Confirm(0);

            TaskManager.DelayNext(100);
            TaskManager.Enqueue(ClickExit);
            return true;
        }

        return false;
    }

    private bool? ClickHighlightBlocks()
    {
        if (TryGetAddonByName<AtkUnitBase>("LotteryDaily", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(addon))
        {
            var helpText = addon->GetTextNodeById(39)->NodeText.ExtractText();

            if (helpText.Contains("格子"))
            {
                ClickHighlightBlock();
                return false;
            }

            TaskManager.DelayNext(250);
            TaskManager.Enqueue(ClickHighlightLine);
            return true;
        }

        return false;
    }

    private static bool? ClickHighlightBlock()
    {
        if (TryGetAddonByName<AddonLotteryDaily>("LotteryDaily", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(&addon->AtkUnitBase))
        {
            var handler = new ClickLotteryDailyDR();
            for (var i = 0; i < 3; i++)
            {
                var rows = new[] { addon->GameBoard.Row1, addon->GameBoard.Row2, addon->GameBoard.Row3 };
                foreach (var row in rows)
                {
                    var block = row[i]->AtkComponentButton.AtkComponentBase.OwnerNode;
                    if (block->AtkResNode is { MultiplyBlue: 0, MultiplyGreen: 100, MultiplyRed: 0 })
                    {
                        handler.Block(BlockToCallbackIndex[block->AtkResNode.NodeID]);
                        return true;
                    }
                }
            }
        }

        return false;
    }


    private bool? ClickHighlightLine()
    {
        if (TryGetAddonByName<AddonLotteryDaily>("LotteryDaily", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(&addon->AtkUnitBase))
        {
            for (var i = 0; i < 8; i++)
            {
                var line = addon->LaneSelector[i]->AtkComponentBase.OwnerNode;

                if (line->AtkResNode is { MultiplyBlue: 0, MultiplyGreen: 100, MultiplyRed: 0 })
                {
                    SelectedLineNumber3D4 = LineToUnkNumber3D4[line->AtkResNode.NodeID];
                    addon->UnkNumber3D4 = SelectedLineNumber3D4;

                    TaskManager.DelayNext(250);
                    TaskManager.Enqueue(ClickConfirm);
                    return true;
                }
            }
        }
        
        return false;
    }

    private bool? ClickConfirm()
    {
        if (TryGetAddonByName<AddonLotteryDaily>("LotteryDaily", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(&addon->AtkUnitBase))
        {
            var handler = new ClickLotteryDailyDR();
            handler.Confirm(SelectedLineNumber3D4);

            TaskManager.DelayNext(100);
            TaskManager.Enqueue(ClickExit);
            return true;
        }

        return false;
    }

    private bool? ClickExit()
    {
        if (TryGetAddonByName<AddonLotteryDaily>("LotteryDaily", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(&addon->AtkUnitBase))
        {
            var clickHandler = new ClickLotteryDailyDR();
            clickHandler.Exit();
            addon->AtkUnitBase.Close(true);

            TaskManager.DelayNext(100);
            TaskManager.Enqueue(() => Click.TrySendClick("select_yes"));
            return true;
        }

        return false;
    }

    private static bool WaitLotteryDailyAddon()
    {
        if (TryGetAddonByName<AtkUnitBase>("LotteryDaily", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(addon))
        {
            var welcomeImageState = addon->GetImageNodeById(4)->AtkResNode.IsVisible;
            var selectBlockTextState = addon->GetTextNodeById(3)->AtkResNode.IsVisible;
            var selectLineTextState = addon->GetTextNodeById(2)->AtkResNode.IsVisible;

            if (!welcomeImageState && !selectBlockTextState && !selectLineTextState) return true;
        }

        return false;
    }

    internal static bool IsEzMiniCactpotInstalled()
    {
        return P.PluginInterface.InstalledPlugins.Any(plugin => plugin is { Name: "ezMiniCactpot", IsLoaded: true });
    }

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddonSetup);

        base.Uninit();
    }
}
