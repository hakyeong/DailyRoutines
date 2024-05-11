using ClickLib.Clicks;
using DailyRoutines.Helpers;
using DailyRoutines.Infos;
using DailyRoutines.Infos.Clicks;
using DailyRoutines.Managers;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace DailyRoutines.Modules;

[PrecedingModule([typeof(AutoTalkSkip)])]
[ModuleDescription("AutoWithdrawRetainersGilsTitle", "AutoWithdrawRetainersGilsDescription", ModuleCategories.雇员)]
public unsafe class AutoWithdrawRetainersGils : DailyModuleBase
{
    public override void Init()
    {
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = false };
    }

    public override void ConfigUI()
    {
        ImGui.BeginDisabled(TaskManager.IsBusy);
        if (ImGui.Button(Service.Lang.GetText("Start"))) GetRetainersGilInfo();
        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("Stop"))) TaskManager.Abort();

        ImGuiOm.HelpMarker(Service.Lang.GetText("AutoWithdrawRetainersGils-Help"));
    }

    private void GetRetainersGilInfo()
    {
        if (Service.Gui.GetAddonByName("RetainerList") == nint.Zero) return;

        var retainerManager = RetainerManager.Instance();
        var retainerCount = retainerManager->GetRetainerCount();

        var totalGilAmount = 0U;
        for (var i = 0U; i < retainerCount; i++) totalGilAmount += retainerManager->GetRetainerBySortedIndex(i)->Gil;

        if (totalGilAmount <= 0) return;

        for (var i = 0; i < retainerCount; i++)
        {
            if (retainerManager->GetRetainerBySortedIndex((uint)i)->Gil == 0) continue;

            EnqueueSingleRetainer(i);
            TaskManager.DelayNext(100);
        }
    }


    private void EnqueueSingleRetainer(int index)
    {
        TaskManager.Enqueue(() => ClickSpecificRetainer(index));

        TaskManager.Enqueue(() => ClickHelper.SelectString("金币管理"));

        TaskManager.DelayNext(100);
        TaskManager.Enqueue(WithdrawAllGils);

        TaskManager.Enqueue(() => ClickHelper.SelectString("返回"));
    }

    private static bool? ClickSpecificRetainer(int index)
    {
        if (!TryGetAddonByName<AtkUnitBase>("RetainerList", out var addon) || !IsAddonAndNodesReady(addon))
            return false;
        ClickRetainerList.Using((nint)addon).Retainer(index);
        return true;
    }

    private static bool? WithdrawAllGils()
    {
        if (!TryGetAddonByName<AtkUnitBase>("Bank", out var addon) || !IsAddonAndNodesReady(addon)) return false;

        var retainerGils = addon->AtkValues[6].Int;
        var handler = new ClickBank();

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
}
