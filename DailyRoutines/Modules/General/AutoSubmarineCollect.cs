using System;
using System.Numerics;
using System.Text.RegularExpressions;
using ClickLib;
using DailyRoutines.Clicks;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.AddonLifecycle;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using ECommons.Automation;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoSubmarineCollectTitle", "AutoSubmarineCollectDescription", ModuleCategories.General)]
public partial class AutoSubmarineCollect : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithUI => true;

    private static TaskManager? TaskManager;
    private static int CurrentIndex;

    public void Init()
    {
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 10000, ShowDebug = false };
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, "SelectYesno", AlwaysYes);
        Service.Chat.ChatMessage += OnErrorText;
    }

    public void UI()
    {
        var infoImageState = ThreadLoadImageHandler.TryGetTextureWrap(
            "https://raw.githubusercontent.com/AtmoOmen/DailyRoutines/main/imgs/AutoSubmarineCollect-1.png",
            out var imageHandler);

        ImGui.TextColored(ImGuiColors.ParsedOrange, Service.Lang.GetText("AutoSubmarineCollect-WhatIsTheList"));

        ImGui.SameLine();
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.TextDisabled(FontAwesomeIcon.InfoCircle.ToIconString());
        ImGui.PopFont();

        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            if (infoImageState)
                ImGui.Image(imageHandler.ImGuiHandle, new Vector2(400, 222));
            else
                ImGui.TextDisabled($"{Service.Lang.GetText("ImageLoading")}...");
            ImGui.EndTooltip();
        }

        ImGui.BeginDisabled(TaskManager.IsBusy);
        if (ImGui.Button(Service.Lang.GetText("AutoSubmarineCollect-Start"))) GetSubmarineInfos();
        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("AutoSubmarineCollect-Stop"))) TaskManager.Abort();
    }

    private static void AlwaysYes(AddonEvent type, AddonArgs args)
    {
        if (!TaskManager.IsBusy || CurrentIndex == 0) return;

        Click.SendClick("select_yes");
    }

    internal static unsafe bool? GetSubmarineInfos()
    {
        if (TryGetAddonByName<AtkUnitBase>("SelectString", out var addon) && HelpersOm.IsAddonAndNodesReady(addon))
        {
            var infoText = addon->GetTextNodeById(2)->NodeText.ExtractText();
            if (string.IsNullOrEmpty(infoText))
            {
                TaskManager.Abort();
                return false;
            }

            var submarineCount = int.TryParse(SubmarineInfoRegex().Match(infoText).Groups[1].Value, out var denominator)
                                     ? denominator
                                     : -1;

            if (submarineCount == -1)
            {
                Service.Chat.PrintError(Service.Lang.GetText("AutoSubmarineCollect-FailGettingSubmarineInfo"));
                return false;
            }

            // 索引从 1 开始
            var listComponent = ((AddonSelectString*)addon)->PopupMenu.PopupMenu.List->AtkComponentBase.UldManager
                .NodeList;
            for (var i = 1; i <= submarineCount; i++)
            {
                var stateText =
                    listComponent[i]->GetAsAtkComponentNode()->Component->UldManager.NodeList[3]->GetAsAtkTextNode()->
                        NodeText.ExtractText();
                if (stateText.Contains("探索完成"))
                {
                    CurrentIndex = i;
                    EnqueueSubmarineCollect(CurrentIndex);
                    return true;
                }
            }
        }

        return false;
    }

    private static unsafe void OnErrorText(Dalamud.Game.Text.XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if (!TaskManager.IsBusy || !message.ExtractText().Contains("无法进行出港，需要修理配件") || CurrentIndex == 0) return;

        Service.Log.Debug("准备进行潜水艇修理");

        if (TryGetAddonByName<AtkUnitBase>("AirShipExplorationDetail", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(addon))
        {
            TaskManager.Abort();
            var handler = new ClickAirShipExplorationDetailDR();
            handler.Cancel();
            addon->Close(true);

            TaskManager.Enqueue(() => Click.TrySendClick("select_string4"));
            TaskManager.Enqueue(RepairSubmarines);
        }

        if (TryGetAddonByName<AtkUnitBase>("SelectString", out var addon1) &&
                 HelpersOm.IsAddonAndNodesReady(addon1))
        {
            TaskManager.Abort();
            TaskManager.Enqueue(() => Click.TrySendClick("select_string4"));
            TaskManager.Enqueue(RepairSubmarines);
        }
    }

    private static void EnqueueSubmarineCollect(int index)
    {
        TaskManager.Enqueue(() => Click.TrySendClick($"select_string{index}"));
        TaskManager.Enqueue(ConfirmVoyageResult);
    }

    private static unsafe bool? ConfirmVoyageResult()
    {
        if (TryGetAddonByName<AtkUnitBase>("AirShipExplorationResult", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(addon))
        {
            var handler = new ClickAirShipExplorationResultDR();
            handler.Redeploy();

            addon->Close(true);

            TaskManager.DelayNext(2000);
            TaskManager.Enqueue(CommenceSubmarineVoyage);

            return true;
        }

        return false;
    }

    private static unsafe bool? CommenceSubmarineVoyage()
    {
        if (TryGetAddonByName<AtkUnitBase>("AirShipExplorationDetail", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(addon))
        {
            var handler = new ClickAirShipExplorationDetailDR();
            handler.Commence();

            addon->Close(true);

            TaskManager.DelayNext(3000);
            TaskManager.Enqueue(GetSubmarineInfos);

            return true;
        }

        return false;
    }

    private static unsafe bool? RepairSubmarines()
    {
        if (TryGetAddonByName<AtkUnitBase>("CompanyCraftSupply", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(addon))
        {
            var handler = new ClickCompanyCraftSupplyDR();

            // 全修
            for (var i = 0; i < 3; i++)
            {
                var i1 = i;
                TaskManager.Enqueue(() => RepairSingleSubmarine(i1));
            }

            TaskManager.Enqueue(() => Click.TrySendClick("select_string2"));
            TaskManager.Enqueue(ConfirmVoyageResult);

            return true;
        }


        return false;
    }

    private static unsafe bool? RepairSingleSubmarine(int index)
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

    public void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(AlwaysYes);
        Service.Chat.ChatMessage -= OnErrorText;
        TaskManager?.Abort();
    }


    [GeneratedRegex("探索机体数：\\d+/(\\d+)")]
    private static partial Regex SubmarineInfoRegex();
}
