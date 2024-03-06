using ClickLib;
using ClickLib.Clicks;
using DailyRoutines.Clicks;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoWithdrawRetainersGilsTitle", "AutoWithdrawRetainersGilsDescription", ModuleCategories.Retainer)]
public unsafe class AutoWithdrawRetainersGils : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithConfigUI => true;

    private static TaskManager? TaskManager;

    public void Init()
    {
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = false };
    }

    public void ConfigUI()
    {
        ImGui.BeginDisabled(TaskManager.IsBusy);
        if (ImGui.Button(Service.Lang.GetText("Start"))) GetRetainersGilInfo();
        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("Stop"))) TaskManager.Abort();

        ImGuiOm.HelpMarker(Service.Lang.GetText("AutoWithdrawRetainersGils-Help"));
    }

    public void OverlayUI() { }

    private static void GetRetainersGilInfo()
    {
        if (Service.Gui.GetAddonByName("RetainerList") == nint.Zero) return;

        var retainerManager = RetainerManager.Instance();
        var retainerCount = retainerManager->GetRetainerCount();

        var totalGilAmount = 0U;
        for (var i = 0U; i < retainerCount; i++) totalGilAmount += retainerManager->GetRetainerBySortedIndex(i)->Gil;

        Service.Log.Debug($"当前 {retainerCount} 个雇员共有 {totalGilAmount} 金币,");

        if (totalGilAmount <= 0) return;

        for (var i = 0; i < retainerCount; i++)
        {
            if (retainerManager->GetRetainerBySortedIndex((uint)i)->Gil == 0) continue;

            EnqueueSingleRetainer(i);
            TaskManager.DelayNext(100);
        }
    }


    private static void EnqueueSingleRetainer(int index)
    {
        // 点击指定雇员
        TaskManager.Enqueue(() => ClickSpecificRetainer(index));
        // 点击金币管理
        TaskManager.Enqueue(() => Click.TrySendClick("select_string2"));
        // 取出所有金币
        TaskManager.DelayNext(100);
        TaskManager.Enqueue(WithdrawAllGils);
        // 回到雇员列表
        TaskManager.Enqueue(() => Click.TrySendClick("select_string13"));
    }

    private static bool? ClickSpecificRetainer(int index)
    {
        if (TryGetAddonByName<AtkUnitBase>("RetainerList", out var addon) && HelpersOm.IsAddonAndNodesReady(addon))
        {
            var handler = new ClickRetainerList();
            handler.Retainer(index);
            return true;
        }

        return false;
    }

    private static bool? WithdrawAllGils()
    {
        if (TryGetAddonByName<AtkUnitBase>("Bank", out var addon) && HelpersOm.IsAddonAndNodesReady(addon))
        {
            var retainerGils = addon->AtkValues[6].Int;
            var handler = new ClickBankDR();

            if (retainerGils == 0)
                handler.Cancel();
            else
            {
                handler.DepositInput((uint)retainerGils);
                handler.Confirm();
            }

            addon->Close(true);
            return true;
        }

        return false;
    }

    public void Uninit()
    {
        TaskManager?.Abort();
    }
}
