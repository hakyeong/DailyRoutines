using ClickLib.Clicks;
using DailyRoutines.Helpers;
using DailyRoutines.Managers;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using DailyRoutines.Infos;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoRetainerRefreshTitle", "AutoRetainerRefreshDescription", ModuleCategories.雇员)]
public unsafe class AutoRetainerRefresh : DailyModuleBase
{
    private static AtkUnitBase* RetainerList => (AtkUnitBase*)Service.Gui.GetAddonByName("RetainerList");

    public override void Init()
    {
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = false };
    }

    public override void ConfigUI()
    {
        ImGui.BeginDisabled(TaskManager.IsBusy);
        if (ImGui.Button(Service.Lang.GetText("Start")))
            EnqueueAllRetainersInList();
        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("Stop")))
            TaskManager.Abort();
    }

    private void EnqueueAllRetainersInList()
    {
        if (RetainerList == null || !IsAddonAndNodesReady(RetainerList)) return;

        var retainerManager = RetainerManager.Instance();
        for (var i = 0; i < retainerManager->GetRetainerCount(); i++)
        {
            var index = i;
            TaskManager.Enqueue(() => RetainerList != null && IsAddonAndNodesReady(RetainerList));
            TaskManager.Enqueue(() => ClickRetainerList.Using((nint)RetainerList).Retainer(index));
            TaskManager.Enqueue(() => ClickHelper.SelectString(LuminaCache.GetRow<Addon>(2378).Text.RawString));
            TaskManager.DelayNext(100);
            TaskManager.Enqueue(ExitRetainerInventory);
            TaskManager.Enqueue(() => ClickHelper.SelectString(LuminaCache.GetRow<Addon>(2383).Text.RawString));
        }
    }

    private static bool? ExitRetainerInventory()
    {
        var agent = AgentModule.Instance()->GetAgentByInternalId(AgentId.Retainer);
        var agent2 = AgentModule.Instance()->GetAgentByInternalId(AgentId.Inventory);
        if (agent == null || agent2 == null || !agent->IsAgentActive()) return false;

        var addon = RaptureAtkUnitManager.Instance()->GetAddonById((ushort)agent->GetAddonID());
        var addon2 = RaptureAtkUnitManager.Instance()->GetAddonById((ushort)agent2->GetAddonID());

        if (addon != null) addon->Close(true);
        if (addon2 != null) AddonHelper.Callback(addon2, true, -1);
        return true;
    }
}
