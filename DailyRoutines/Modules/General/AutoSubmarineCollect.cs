using System.Numerics;
using System.Text.RegularExpressions;
using ClickLib;
using ClickLib.Clicks;
using DailyRoutines.Clicks;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoSubmarineCollectTitle", "AutoSubmarineCollectDescription", ModuleCategories.General)]
public unsafe partial class AutoSubmarineCollect : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithConfigUI => true;

    private static TaskManager? TaskManager;

    public void Init()
    {
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 30000, ShowDebug = true };
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, "SelectYesno", AlwaysYes);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, "AirShipExplorationResult", OnExplorationResult);


        Service.Chat.ChatMessage += OnErrorText;
    }

    public void ConfigUI()
    {
        PreviewImageWithHelpText(Service.Lang.GetText("AutoSubmarineCollect-WhatIsTheList"),
                                 "https://raw.githubusercontent.com/AtmoOmen/DailyRoutines/main/imgs/AutoSubmarineCollect-1.png",
                                 new Vector2(400, 222));

        ImGui.BeginDisabled(TaskManager.IsBusy);
        if (ImGui.Button(Service.Lang.GetText("Start"))) GetSubmarineInfos();
        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("Stop"))) TaskManager.Abort();
    }

    public void OverlayUI() { }

    private static void AlwaysYes(AddonEvent type, AddonArgs args)
    {
        if (!TaskManager.IsBusy) return;
        Click.SendClick("select_yes");
    }

    // 无法出港报错 -> 修理潜水艇
    private static void OnErrorText(
        XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if (!TaskManager.IsBusy || !message.ExtractText().Contains("需要修理配件")) return;

        TaskManager.Abort();
        TaskManager.Enqueue(ReadyToRepairSubmarines);
    }

    // 航程结果 -> 再次出发
    private void OnExplorationResult(AddonEvent type, AddonArgs args)
    {
        if (TryGetAddonByName<AtkUnitBase>("AirShipExplorationResult", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(addon))
        {
            var handler = new ClickAirShipExplorationResultDR((nint)addon);
            handler.Redeploy();

            addon->IsVisible = false;
        }
    }

    private static bool? GetSubmarineInfos()
    {
        if (TryGetAddonByName<AtkUnitBase>("SelectString", out var addon) && HelpersOm.IsAddonAndNodesReady(addon))
        {
            if (!HelpersOm.TryScanSelectStringText(addon, "探索完成", out var index))
            {
                TaskManager.Abort();
                return true;
            }

            Click.SendClick($"select_string{index + 1}");

            // 会有 Toast
            TaskManager.DelayNext(2000);
            TaskManager.Enqueue(CommenceSubmarineVoyage);

            return true;
        }

        return false;
    }

    private static bool? CommenceSubmarineVoyage()
    {
        if (TryGetAddonByName<AtkUnitBase>("AirShipExplorationDetail", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(addon))
        {
            var handler = new ClickAirShipExplorationDetailDR();
            handler.Commence();
            addon->Close(true);

            // 一轮结束, 清理任务; 会有动画
            TaskManager.Abort();
            TaskManager.DelayNext(3000);
            TaskManager.Enqueue(GetSubmarineInfos);

            return true;
        }

        return false;
    }

    internal static bool? ReadyToRepairSubmarines()
    {
        if (TryGetAddonByName<AtkUnitBase>("AirShipExplorationDetail", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(addon))
        {
            var handler = new ClickAirShipExplorationDetailDR();
            handler.Cancel();
            addon->IsVisible = false;

            var selectStringAddon = (AtkUnitBase*)Service.Gui.GetAddonByName("SelectString");
            if (selectStringAddon == null) return false;
            if (!HelpersOm.TryScanSelectStringText(selectStringAddon, "修理", out var index)) return false;

            TaskManager.Enqueue(() => Click.TrySendClick($"select_string{index + 1}"));
            TaskManager.Enqueue(RepairSubmarines);
            return true;
        }


        if (TryGetAddonByName<AtkUnitBase>("SelectString", out var addon1) && HelpersOm.IsAddonAndNodesReady(addon1))
        {
            if (!HelpersOm.TryScanSelectStringText(addon1, "修理", out var index)) return false;

            var handler = new ClickSelectString();
            handler.SelectItem((ushort)index);
            addon1->Close(true);

            TaskManager.Enqueue(RepairSubmarines);
            return true;
        }

        return false;
    }

    private static bool? RepairSubmarines()
    {
        if (TryGetAddonByName<AtkUnitBase>("CompanyCraftSupply", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(addon))
        {
            var handler = new ClickCompanyCraftSupplyDR();

            // 全修
            for (var i = 0; i < 4; i++)
            {
                var i1 = i;
                TaskManager.Enqueue(() => RepairSingleSubmarine(i1));
                TaskManager.DelayNext(250);
            }

            TaskManager.Enqueue(handler.Close);
            TaskManager.Enqueue(() => addon->Close(true));
            TaskManager.DelayNext(100);
            TaskManager.Enqueue(ClickPreviousVoyageLog);

            return true;
        }


        return false;
    }

    private static bool? RepairSingleSubmarine(int index)
    {
        if (TryGetAddonByName<AtkUnitBase>("CompanyCraftSupply", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(addon))
        {
            var handler = new ClickCompanyCraftSupplyDR();
            handler.Component(index);

            return true;
        }

        return false;
    }

    private static bool? ClickPreviousVoyageLog()
    {
        if (TryGetAddonByName<AtkUnitBase>("AirShipExplorationDetail", out var detailAddon) &&
            HelpersOm.IsAddonAndNodesReady(detailAddon))
        {
            TaskManager.Abort();
            TaskManager.Enqueue(CommenceSubmarineVoyage);

            return true;
        }

        if (TryGetAddonByName<AtkUnitBase>("SelectString", out var addon) && HelpersOm.IsAddonAndNodesReady(addon))
        {
            if (!HelpersOm.TryScanSelectStringText(addon, "上次的远航报告", out var index))
            {
                TaskManager.Abort();
                return true;
            }

            if (Click.TrySendClick($"select_string{index + 1}"))
            {
                addon->Close(true);

                TaskManager.DelayNext(100);
                TaskManager.Enqueue(CommenceSubmarineVoyage);
                return true;
            }
        }

        return false;
    }

    public void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnExplorationResult);
        Service.AddonLifecycle.UnregisterListener(AlwaysYes);
        Service.Chat.ChatMessage -= OnErrorText;
        TaskManager?.Abort();
    }


    [GeneratedRegex("探索机体数：\\d+/(\\d+)")]
    private static partial Regex SubmarineInfoRegex();
}
