using System;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.AddonLifecycle;
using ClickLib.Clicks;
using DailyRoutines.Windows;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using Dalamud.Interface.Colors;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoSellCardsConfirmTitle", "AutoSellCardsConfirmDescription", ModuleCategories.GoldSaucer)]
public class AutoSellCardsConfirm : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithConfigUI => false;
    internal static Overlay? Overlay { get; private set; }

    private static TaskManager? TaskManager;
    private static bool IsOnProcess;

    public void Init()
    {
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = false };
        Overlay ??= new Overlay(this);

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "ShopCardDialog", OnAddon);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "TripleTriadCoinExchange", OnAddon);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "TripleTriadCoinExchange", OnAddon);
    }

    public void ConfigUI() { }

    public unsafe void OverlayUI()
    {
        var addon = (AtkUnitBase*)Service.Gui.GetAddonByName("TripleTriadCoinExchange");
        if (addon == null) return;

        var pos = new Vector2(addon->GetX() - ImGui.GetWindowSize().X, addon->GetY() + 6);
        ImGui.SetWindowPos(pos);

        ImGui.TextColored(ImGuiColors.DalamudYellow, Service.Lang.GetText("AutoSellCardsConfirmTitle"));

        ImGui.Separator();

        ImGui.BeginDisabled(IsOnProcess);
        if (ImGui.Button(Service.Lang.GetText("AutoSellCardsConfirm-Start"))) StartHandOver();
        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("AutoSellCardsConfirm-Stop"))) EndHandOver();
    }

    private unsafe void OnAddon(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)args.Addon;
        if (addon == null) return;

        if (args.AddonName == "ShopCardDialog")
        {
            var handler = new ClickShopCardDialog();
            handler.Sell();
        }
        else
        {
            switch (type)
            {
                case AddonEvent.PostSetup:
                    Overlay.IsOpen = true;
                    break;
                case AddonEvent.PreFinalize:
                    Overlay.IsOpen = false;
                    EndHandOver();
                    break;
            }
        }
    }

    private unsafe bool? StartHandOver()
    {
        if (TryGetAddonByName<AtkUnitBase>("TripleTriadCoinExchange", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(addon))
        {
            var cardsAmount = addon->AtkValues[1].Int;
            if (cardsAmount == 0)
            {
                EndHandOver();
                return true;
            }

            var isCardInDeck = Convert.ToBoolean(addon->AtkValues[204].Byte);
            if (!isCardInDeck)
            {
                Service.Chat.Print(Service.Lang.GetSeString("AutoSellCardsConfirm-CurrentCardNotInDeckMessage"));
                EndHandOver();
                return true;
            }

            TaskManager.Enqueue(() => Callback.Fire(addon, true, 0, 0, 0));
            IsOnProcess = true;
            TaskManager.DelayNext(100);
            TaskManager.Enqueue(StartHandOver);

            return true;
        }

        return false;
    }

    private void EndHandOver()
    {
        TaskManager?.Abort();
        IsOnProcess = false;
    }

    public void Uninit()
    {
        EndHandOver();

        Service.AddonLifecycle.UnregisterListener(OnAddon);

        if (P.WindowSystem.Windows.Contains(Overlay)) P.WindowSystem.RemoveWindow(Overlay);
        Overlay = null;
    }
}
