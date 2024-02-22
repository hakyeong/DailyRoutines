using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using ClickLib.Clicks;
using DailyRoutines.Clicks;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using DailyRoutines.Windows;
using Dalamud.Game.AddonLifecycle;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoExpertDeliveryTitle", "AutoExpertDeliveryDescription", ModuleCategories.General)]
public unsafe class AutoExpertDelivery : IDailyModule
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
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "GrandCompanySupplyList", OnAddonSupplyList);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "GrandCompanySupplyList", OnAddonSupplyList);
    }

    public void ConfigUI() { }

    public void OverlayUI()
    {
        var addon = (AtkUnitBase*)Service.Gui.GetAddonByName("GrandCompanySupplyList");
        if (addon == null) return;

        var pos = new Vector2(addon->GetX() - ImGui.GetWindowSize().X, addon->GetY() + 6);
        ImGui.SetWindowPos(pos);

        ImGui.TextColored(ImGuiColors.DalamudYellow, Service.Lang.GetText("AutoExpertDeliveryTitle"));
        ImGui.PushTextWrapPos(300f * ImGuiHelpers.GlobalScale);
        ImGui.TextDisabled(Service.Lang.GetText("AutoExpertDeliveryDescription"));
        ImGui.PopTextWrapPos();

        ImGui.Separator();

        ImGui.BeginDisabled(IsOnProcess);
        if (ImGui.Button(Service.Lang.GetText("AutoExpertDelivery-Start"))) StartHandOver();
        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("AutoExpertDelivery-Stop"))) EndHandOver();
    }

    private static void OnAddonSupplyList(AddonEvent type, AddonArgs args)
    {
        switch (type)
        {
            case AddonEvent.PostSetup:
                Overlay.IsOpen = true;
                break;
            case AddonEvent.PreFinalize:
                Overlay.IsOpen = false;
                break;
        }
    }

    private static void OnAddonSupplyReward(AddonEvent type, AddonArgs args)
    {
        if (TryGetAddonByName<AtkUnitBase>("GrandCompanySupplyReward", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(addon))
        {
            var handler = new ClickGrandCompanySupplyReward();
            handler.Deliver();
        }
    }

    private static void StartHandOver()
    {
        if (IsSealsReachTheCap())
        {
            EndHandOver();
            return;
        }

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "GrandCompanySupplyReward", OnAddonSupplyReward);
        var handler = new ClickGrandCompanySupplyListDR();
        handler.ExpertDelivery();
        TaskManager.Enqueue(ClickFirstItem);

        IsOnProcess = true;
    }

    private static void EndHandOver()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddonSupplyReward);
        TaskManager?.Abort();
        IsOnProcess = false;
    }

    // (即将)达到限额 - true
    private static bool IsSealsReachTheCap()
    {
        if (TryGetAddonByName<AddonGrandCompanySupplyList>("GrandCompanySupplyList", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(&addon->AtkUnitBase))
        {
            var isNoItems = addon->AtkUnitBase.GetTextNodeById(9)->AtkResNode.IsVisible;
            if (isNoItems) return true;

            var amountText =
                Marshal.PtrToStringUTF8((nint)AtkStage.GetSingleton()->GetStringArrayData()[32]->StringArray[2]);
            var parts = amountText.Split('/');
            var currentAmount = int.Parse(parts[0].Replace(",", ""));
            var capAmount = int.Parse(parts[1].Replace(",", ""));

            var firstItem =
                addon->ExpertDeliveryList->AtkComponentBase.UldManager.NodeList[2]->GetAsAtkComponentNode()->Component->
                    UldManager.NodeList[4]->GetAsAtkTextNode()->NodeText.ExtractText();
            if (string.IsNullOrEmpty(firstItem))
            {
                TaskManager.Abort();
                return false;
            }

            var firstItemAmount = int.Parse(firstItem);

            return firstItemAmount + currentAmount > capAmount;
        }

        return true;
    }

    private static bool? ClickFirstItem()
    {
        if (TryGetAddonByName<AddonGrandCompanySupplyList>("GrandCompanySupplyList", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(&addon->AtkUnitBase))
        {
            var handler = new ClickGrandCompanySupplyListDR();
            handler.ExpertDelivery();
            handler.ItemEntry(0);

            TaskManager.Enqueue(CheckSealsState);

            return true;
        }

        return false;
    }

    private static bool? CheckSealsState()
    {
        if (TryGetAddonByName<AddonGrandCompanySupplyList>("GrandCompanySupplyList", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(&addon->AtkUnitBase))
        {
            StartHandOver();

            return true;
        }

        return false;
    }

    public void Uninit()
    {
        EndHandOver();

        if (P.WindowSystem.Windows.Contains(Overlay)) P.WindowSystem.RemoveWindow(Overlay);
        Overlay = null;

        Service.AddonLifecycle.UnregisterListener(OnAddonSupplyList);
    }
}
