using System;
using System.Numerics;
using DailyRoutines.Helpers;
using DailyRoutines.Managers;
using DailyRoutines.Windows;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Colors;

using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoSellCardsConfirmTitle", "AutoSellCardsConfirmDescription", ModuleCategories.金碟)]
public class AutoSellCardsConfirm : DailyModuleBase
{
    public override void Init()
    {
        TaskHelper ??= new TaskHelper { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = false };
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
        ImGui.BeginDisabled(TaskHelper.IsBusy);
        if (ImGui.Button(Service.Lang.GetText("Start"))) StartHandOver();
        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("Stop"))) TaskHelper.Abort();
    }

    private unsafe void OnAddon(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)args.Addon;
        if (addon == null) return;

        if (args.AddonName == "ShopCardDialog")
        {
            AddonHelper.Callback(addon, true, 0, addon->AtkValues[6].UInt);
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
                TaskHelper?.Abort();
                break;
        }
    }

    private unsafe bool? StartHandOver()
    {
        if (!Throttler.Throttle("AutoSellCardsConfirm")) return false;

        if (Service.Gui.GetAddonByName("ShopCardDialog") != nint.Zero)
        {
            TaskHelper.Abort();
            return true;
        }

        if (!TryGetAddonByName<AtkUnitBase>("TripleTriadCoinExchange", out var addon) ||
            !IsAddonAndNodesReady(addon)) return false;

        var cardsAmount = addon->AtkValues[1].Int;
        if (cardsAmount is 0)
        {
            TaskHelper?.Abort();
            return true;
        }

        var isCardInDeck = Convert.ToBoolean(addon->AtkValues[204].Byte);
        if (!isCardInDeck)
        {
            var message = new SeStringBuilder().Append(DRPrefix).Append(" ")
                                               .Append(Service.Lang.GetSeString(
                                                           "AutoSellCardsConfirm-CurrentCardNotInDeckMessage")).Build();

            Service.Chat.Print(message);

            TaskHelper?.Abort();
            return true;
        }

        TaskHelper.Enqueue(() => AddonHelper.Callback(addon, true, 0, 0, 0));
        TaskHelper.DelayNext(100);
        TaskHelper.Enqueue(StartHandOver);

        return true;
    }

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddon);

        base.Uninit();
    }
}
