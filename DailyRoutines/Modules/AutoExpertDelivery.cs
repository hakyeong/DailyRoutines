using System.Linq;
using System.Runtime.InteropServices;
using ClickLib.Clicks;
using DailyRoutines.Clicks;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using DailyRoutines.Windows.Overlays;
using Dalamud.Game.AddonLifecycle;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoExpertDeliveryTitle", "AutoExpertDeliveryDescription", ModuleCategories.General)]
public class AutoExpertDelivery : IDailyModule
{
    public bool Initialized { get; set; }
    internal static AutoExpertDeliveryOverlay? Overlay { get; private set; }

    private static TaskManager? TaskManager;
    public static bool IsOnProcess;

    public void Init()
    {
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = false };
        Overlay ??= new AutoExpertDeliveryOverlay();
        if (!P.WindowSystem.Windows.Contains(Overlay)) P.WindowSystem.AddWindow(Overlay);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, "GrandCompanySupplyList", OnAddonSupplyList);

        Initialized = true;
    }

    public void UI()
    {
        ImGui.BeginDisabled(IsOnProcess);
        if (ImGui.Button(Service.Lang.GetText("AutoExpertDelivery-Start"))) StartHandOver();
        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("AutoExpertDelivery-Stop"))) EndHandOver();
    }

    private static void OnAddonSupplyList(AddonEvent type, AddonArgs args)
    {
        Overlay.IsOpen = IsAddonSupplyListReady();
    }

    private static unsafe void OnAddonSupplyReward(AddonEvent type, AddonArgs args)
    {
        if (TryGetAddonByName<AtkUnitBase>("GrandCompanySupplyReward", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(addon))
        {
            var handler = new ClickGrandCompanySupplyReward();
            handler.Deliver();
        }
    }

    private static unsafe bool IsAddonSupplyListReady()
    {
        return TryGetAddonByName<AtkUnitBase>("GrandCompanySupplyList", out var addon) &&
               HelpersOm.IsAddonAndNodesReady(addon);
    }

    public static void StartHandOver()
    {
        if (IsSealsReachTheCap())
        {
            EndHandOver();
            return;
        }

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "GrandCompanySupplyReward", OnAddonSupplyReward);

        TaskManager.Enqueue(ClickFirstItem);

        IsOnProcess = true;
    }

    public static void EndHandOver()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddonSupplyReward);
        TaskManager.Abort();
        IsOnProcess = false;
    }

    // (即将)达到限额 - true
    private static unsafe bool IsSealsReachTheCap()
    {
        if (TryGetAddonByName<AddonGrandCompanySupplyList>("GrandCompanySupplyList", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(&addon->AtkUnitBase))
        {
            var isNoItems = addon->AtkUnitBase.GetTextNodeById(9)->AtkResNode.IsVisible;
            if (isNoItems) return true;

            var amountText = Marshal.PtrToStringUTF8((nint)AtkStage.GetSingleton()->GetStringArrayData()[32]->StringArray[2]);
            var parts = amountText.Split('/');
            var currentAmount = int.Parse(parts[0].Replace(",", ""));
            var capAmount = int.Parse(parts[1].Replace(",", ""));

            var firstItem =
                addon->ExpertDeliveryList->AtkComponentBase.UldManager.NodeList[2]->GetAsAtkComponentNode()->Component->
                    UldManager.NodeList[4]->GetAsAtkTextNode()->NodeText.ExtractText();
            if (string.IsNullOrEmpty(firstItem)) return true; // 不存在第一件物品
            var firstItemAmount = int.Parse(firstItem);

            return firstItemAmount + currentAmount > capAmount;
        }

        return true;
    }

    private static unsafe bool? ClickFirstItem()
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

    private static unsafe bool? CheckSealsState()
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
        Overlay?.Dispose();
        if (P.WindowSystem.Windows.Contains(Overlay)) P.WindowSystem.RemoveWindow(Overlay);
        Service.AddonLifecycle.UnregisterListener(OnAddonSupplyList);

        Initialized = false;
    }
}
