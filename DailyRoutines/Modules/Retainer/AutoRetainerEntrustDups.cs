using ClickLib.Clicks;
using DailyRoutines.Helpers;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Command;
using Dalamud.Memory;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using TaskManager = ECommons.Automation.TaskManager;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoRetainerEntrustDupsTitle", "AutoRetainerEntrustDupsDescription", ModuleCategories.Retainer)]
public unsafe class AutoRetainerEntrustDups : DailyModuleBase
{
    private static AtkUnitBase* RetainerList => (AtkUnitBase*)Service.Gui.GetAddonByName("RetainerList");

    private static bool IsEnableCommand;
    private const string Command = "entrustdups";

    public override void Init()
    {
        AddConfig(nameof(IsEnableCommand), false);
        IsEnableCommand = GetConfig<bool>(nameof(IsEnableCommand));

        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = int.MaxValue, ShowDebug = false };

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "RetainerItemTransferList", OnAddon);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "RetainerItemTransferProgress", OnAddon);

        if (IsEnableCommand)
            Service.CommandManager.AddSubCommand(
                Command,
                new CommandInfo(OnCommand)
                    { ShowInHelp = true, HelpMessage = Service.Lang.GetText("AutoRetainerEntrustDups-CommandHelp") });
    }

    public override void ConfigUI()
    {
        if (ImGui.Checkbox(Service.Lang.GetText("AutoRetainerEntrustDups-AddCommand", Command), ref IsEnableCommand))
        {
            if (IsEnableCommand)
                Service.CommandManager.AddSubCommand(
                    Command,
                    new CommandInfo(OnCommand)
                    {
                        ShowInHelp = true, HelpMessage = Service.Lang.GetText("AutoRetainerEntrustDups-CommandHelp")
                    });
            else Service.CommandManager.RemoveSubCommand(Command);
            UpdateConfig(nameof(IsEnableCommand), IsEnableCommand);
        }

        ImGuiOm.HelpMarker(Service.Lang.GetText(Service.Lang.GetText("AutoRetainerEntrustDups-CommandHelp")));

        ImGui.BeginDisabled(TaskManager.IsBusy);
        if (ImGui.Button(Service.Lang.GetText("Start")))
            EnqueueAllRetainer();
        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("Stop")))
            TaskManager.Abort();
    }

    private void EnqueueAllRetainer()
    {
        if (RetainerList == null) return;

        var retainerManager = RetainerManager.Instance();
        for (var i = 0; i < retainerManager->GetRetainerCount(); i++) EnqueueSingleRetainer(i);
    }

    private void EnqueueSingleRetainer(int index)
    {
        TaskManager.Enqueue(() => RetainerList != null && IsAddonAndNodesReady(RetainerList));
        TaskManager.Enqueue(() => ClickRetainerList.Using((nint)RetainerList).Retainer(index));
        TaskManager.Enqueue(() => ClickHelper.SelectString(LuminaCache.GetRow<Addon>(2378).Text.RawString));
        TaskManager.Enqueue(ClickEntrustDuplicates);
        TaskManager.DelayNext(500);
        TaskManager.Enqueue(ExitRetainerInventory);
        TaskManager.Enqueue(() => ClickHelper.SelectString(LuminaCache.GetRow<Addon>(2383).Text.RawString));
    }

    private static bool? ClickEntrustDuplicates()
    {
        if (!EzThrottler.Throttle("AutoRetainerEntrustDups", 100)) return false;
        var agent = AgentModule.Instance()->GetAgentByInternalId(AgentId.Retainer);
        if (agent == null || !agent->IsAgentActive()) return false;

        var addon = RaptureAtkUnitManager.Instance()->GetAddonById((ushort)agent->GetAddonID());
        if (addon == null) return false;

        AddonHelper.Callback(addon, true, 0);
        return true;
    }

    private static bool? WaitForEntrustFinishing()
    {
        if (!EzThrottler.Throttle("AutoRetainerEntrustDups", 100)) return false;
        if (!TryGetAddonByName<AtkUnitBase>("RetainerItemTransferProgress", out var addon) ||
            !IsAddonAndNodesReady(addon)) return false;

        var progressText = MemoryHelper.ReadSeStringNullTerminated((nint)addon->AtkValues[0].String).ExtractText();
        if (string.IsNullOrWhiteSpace(progressText)) return false;

        if (progressText.Contains(LuminaCache.GetRow<Addon>(13528).Text.RawString))
        {
            AddonHelper.Callback(addon, true, -2);
            addon->Close(true);
            return true;
        }

        return false;
    }

    private static bool? ExitRetainerInventory()
    {
        var agent = AgentModule.Instance()->GetAgentByInternalId(AgentId.Retainer);
        var agent2 = AgentModule.Instance()->GetAgentByInternalId(AgentId.Inventory);
        if (agent == null || agent2 == null || !agent->IsAgentActive()) return false;

        var addon = RaptureAtkUnitManager.Instance()->GetAddonById((ushort)agent->GetAddonID());
        var addon2 = RaptureAtkUnitManager.Instance()->GetAddonById((ushort)agent2->GetAddonID());
        if (addon == null) return false;

        addon->Close(true);
        if (addon2 != null) addon2->Close(true);
        return true;
    }

    private void OnAddon(AddonEvent type, AddonArgs args)
    {
        if (!TaskManager.IsBusy) return;
        switch (args.AddonName)
        {
            case "RetainerItemTransferList":
                AddonHelper.Callback((AtkUnitBase*)args.Addon, true, 1);
                break;
            case "RetainerItemTransferProgress":
                TaskManager.EnqueueImmediate(WaitForEntrustFinishing);
                break;
        }
    }

    private void OnCommand(string command, string arguments)
    {
        if (TaskManager.IsBusy) return;
        EnqueueAllRetainer();
    }

    public override void Uninit()
    {
        Service.CommandManager.RemoveSubCommand(Command);
        Service.AddonLifecycle.UnregisterListener(OnAddon);

        base.Uninit();
    }
}
