using System;
using System.Numerics;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using DailyRoutines.Windows;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Interface.Colors;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoSellCardsConfirmTitle", "AutoSellCardsConfirmDescription", ModuleCategories.GoldSaucer)]
public class AutoSellCardsConfirm : DailyModuleBase
{
    public override void Init()
    {
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = false };
        Overlay ??= new Overlay(this);
        Overlay.Flags |= ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoBackground;

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "ShopCardDialog", OnAddon);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "TripleTriadCoinExchange", OnAddon);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "TripleTriadCoinExchange", OnAddon);
    }

    public override unsafe void OverlayUI()
    {
        var addon = (AtkUnitBase*)Service.Gui.GetAddonByName("TripleTriadCoinExchange");
        if (addon == null) return;

        var title = addon->GetNodeById(12)->GetComponent()->GetTextNodeById(3);
        var pos = new Vector2(title->ScreenX + title->Width, title->ScreenY - 3);
        ImGui.SetWindowPos(pos);

        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(ImGuiColors.DalamudYellow, Service.Lang.GetText("AutoSellCardsConfirmTitle"));

        ImGui.SameLine();
        ImGui.BeginDisabled(TaskManager.IsBusy);
        if (ImGui.Button(Service.Lang.GetText("Start"))) StartHandOver();
        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("Stop"))) TaskManager.Abort();
    }

    private unsafe void OnAddon(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)args.Addon;
        if (addon == null) return;

        if (args.AddonName == "ShopCardDialog")
        {
            AgentManager.SendEvent(AgentId.TripleTriadCoinExchange, 1, 0, 1);
            addon->FireCloseCallback();
            addon->Close(true);
            return;
        }

        switch (type)
        {
            case AddonEvent.PostSetup:
                Overlay.IsOpen = true;
                break;
            case AddonEvent.PreFinalize:
                Overlay.IsOpen = false;
                TaskManager?.Abort();
                break;
        }
    }

    private unsafe bool? StartHandOver()
    {
        if (TryGetAddonByName<AtkUnitBase>("TripleTriadCoinExchange", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(addon))
        {
            var cardsAmount = addon->AtkValues[1].Int;
            if (cardsAmount is 0)
            {
                TaskManager?.Abort();
                return true;
            }

            var isCardInDeck = Convert.ToBoolean(addon->AtkValues[204].Byte);
            if (!isCardInDeck)
            {
                Service.Chat.Print(Service.Lang.GetSeString("AutoSellCardsConfirm-CurrentCardNotInDeckMessage"));
                TaskManager?.Abort();
                return true;
            }

            TaskManager.Enqueue(() => AddonManager.Callback(addon, true, 0, 0, 0));
            TaskManager.DelayNext(400);
            TaskManager.Enqueue(StartHandOver);

            return true;
        }

        return false;
    }

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddon);

        base.Uninit();
    }
}
