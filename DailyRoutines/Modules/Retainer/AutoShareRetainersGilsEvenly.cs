using System;
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

[ModuleDescription("AutoShareRetainersGilsEvenlyTitle", "AutoShareRetainersGilsEvenlyDescription",
                   ModuleCategories.Retainer)]
public unsafe class AutoShareRetainersGilsEvenly : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithConfigUI => true;

    private static TaskManager? TaskManager;

    private static uint AverageAmount;
    private static int ConfigAdjustMethod;

    public void Init()
    {
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = false };

        Service.Config.AddConfig(this, "AdjustMethod", 0);
        ConfigAdjustMethod = Service.Config.GetConfig<int>(this, "AdjustMethod");
    }

    public void ConfigUI()
    {
        ImGui.BeginDisabled(TaskManager.IsBusy);

        ImGui.RadioButton(Service.Lang.GetText("AutoShareRetainersGilsEvenly-Method1"), ref ConfigAdjustMethod, 0);

        ImGui.SameLine();
        ImGui.RadioButton(Service.Lang.GetText("AutoShareRetainersGilsEvenly-Method2"), ref ConfigAdjustMethod, 1);

        ImGui.SameLine();
        ImGui.Spacing();

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("Start"))) GetRetainersGilInfo();
        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("Stop"))) TaskManager.Abort();

        ImGuiOm.HelpMarker(Service.Lang.GetText("AutoShareRetainersGilsEvenly-Help"));
    }

    public void OverlayUI() { }

    private static void GetRetainersGilInfo()
    {
        if (Service.Gui.GetAddonByName("RetainerList") == nint.Zero) return;

        var retainerManager = RetainerManager.Instance();
        var retainerCount = retainerManager->GetRetainerCount();

        AverageAmount = 0;
        var totalGilAmount = 0U;
        for (var i = 0U; i < retainerCount; i++) totalGilAmount += retainerManager->GetRetainerBySortedIndex(i)->Gil;

        AverageAmount = (uint)Math.Floor(totalGilAmount / (double)retainerCount);
        Service.Log.Debug($"当前 {retainerCount} 个雇员共有 {totalGilAmount} 金币, 平均每个雇员 {AverageAmount} 金币");

        if (AverageAmount <= 1) return;

        switch (ConfigAdjustMethod)
        {
            case 0:
                for (var i = 0; i < retainerCount; i++)
                {
                    EnqueueSingleRetainerMethodFirst(i);
                    TaskManager.DelayNext(100);
                }

                break;
            case 1:
                for (var i = 0; i < retainerCount; i++)
                {
                    EnqueueSingleRetainerMethodSecond(i);
                    TaskManager.DelayNext(100);
                }

                for (var i = 0; i < retainerCount; i++)
                {
                    EnqueueSingleRetainerMethodFirst(i);
                    TaskManager.DelayNext(100);
                }

                break;
        }
    }

    private static void EnqueueSingleRetainerMethodFirst(int index)
    {
        // 点击指定雇员
        TaskManager.Enqueue(() => ClickSpecificRetainer(index));
        // 点击金币管理
        TaskManager.Enqueue(() => Click.TrySendClick("select_string2"));
        // 重新分配金币
        TaskManager.DelayNext(100);
        TaskManager.Enqueue(ReassignGils);
        // 回到雇员列表
        TaskManager.Enqueue(() => Click.TrySendClick("select_string13"));
    }

    private static void EnqueueSingleRetainerMethodSecond(int index)
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

    private static bool? ReassignGils()
    {
        if (TryGetAddonByName<AtkUnitBase>("Bank", out var addon) && HelpersOm.IsAddonAndNodesReady(addon))
        {
            var retainerGils = addon->AtkValues[6].Int;
            var handler = new ClickBankDR();

            if (retainerGils == AverageAmount) // 金币恰好相等
            {
                handler.Cancel();
                addon->Close(true);
                return true;
            }

            if (retainerGils > AverageAmount) // 雇员金币多于平均值
            {
                handler.DepositInput((uint)(retainerGils - AverageAmount));
                handler.Confirm();
                addon->Close(true);
                return true;
            }

            // 雇员金币少于平均值
            handler.Switch();
            handler.DepositInput((uint)(AverageAmount - retainerGils));
            handler.Confirm();
            addon->Close(true);
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
