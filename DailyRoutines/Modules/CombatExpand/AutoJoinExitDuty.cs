using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Runtime.InteropServices;
using ClickLib.Clicks;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using TaskManager = ECommons.Automation.TaskManager;
using DailyRoutines.Helpers;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoJoinExitDutyTitle", "AutoJoinExitDutyDescription", ModuleCategories.CombatExpand)]
public unsafe class AutoJoinExitDuty : DailyModuleBase
{
    private static AtkUnitBase* ContentsFinder => (AtkUnitBase*)Service.Gui.GetAddonByName("ContentsFinder");

    private const string AbandonDutySig = "E8 ?? ?? ?? ?? 48 8B 43 28 B1 01";
    private delegate void AbandonDutyDelagte(bool a1);
    private static AbandonDutyDelagte? AbandonDuty;

    public override void Init()
    {
        AbandonDuty ??= Marshal.GetDelegateForFunctionPointer<AbandonDutyDelagte>(Service.SigScanner.ScanText(AbandonDutySig));

        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 60000, ShowDebug = false };
        Service.CommandManager.AddSubCommand("joinexitduty", 
                                     new CommandInfo(OnCommand) { HelpMessage = Service.Lang.GetText("AutoJoinExitDutyTitle"), ShowInHelp = true });
    }

    private void OnCommand(string command, string arguments)
    {
        if (Flags.BoundByDuty() || !UIState.IsInstanceContentUnlocked(4) ||
            Service.ClientState.LocalPlayer == null || Service.ClientState.LocalPlayer.ClassJob.Id is >= 8 and <= 18) return;

        TaskManager.Abort();
        EnqueueARound();
    }

    private void EnqueueARound()
    {
        TaskManager.Enqueue(OpenContentsFinder);
        TaskManager.Enqueue(CancelSelectedContents);
        TaskManager.Enqueue(SelectDuty);
        TaskManager.Enqueue(CommenceDuty);
        TaskManager.Enqueue(ExitDuty);
    }

    private static bool? OpenContentsFinder()
    {
        if (ContentsFinder != null && IsAddonAndNodesReady(ContentsFinder)) return true;

        AgentModule.Instance()->GetAgentContentsFinder()->AgentInterface.Show();
        return true;
    }

    private static bool? CancelSelectedContents()
    {
        if (ContentsFinder == null || !IsAddonAndNodesReady(ContentsFinder)) return false;

        AddonHelper.Callback(ContentsFinder, true, 12, 1);
        var atkValues = ContentsFinder->AtkValues;
        atkValues[7].UInt = 3;
        atkValues[7].Type = ValueType.UInt;
        ContentsFinder->OnRefresh(ContentsFinder->AtkValuesCount, atkValues);
        return true;
    }

    private static bool? SelectDuty()
    {
        if (!EzThrottler.Throttle("AutoJoinExitDuty-SelectDuty", 100)) return false;
        if (ContentsFinder == null || !IsAddonAndNodesReady(ContentsFinder)) return false;

        var agent = AgentModule.Instance()->GetAgentContentsFinder();
        var instance = FFXIVClientStructs.FFXIV.Client.Game.UI.ContentsFinder.Instance();
        if (agent == null || instance == null) return false;
        if (!instance->IsExplorerMode)
        {
            instance->IsExplorerMode = true;
            return false;
        }

        AddonHelper.Callback(ContentsFinder, true, 1, 1);
        AddonHelper.Callback(ContentsFinder, true, 4, 0, true);
        AddonHelper.Callback(ContentsFinder, true, 4, 1, false);
        AddonHelper.Callback(ContentsFinder, true, 4, 2, false);
        AddonHelper.Callback(ContentsFinder, true, 4, 3, false);

        if (ContentsFinder->AtkValues[7].UInt != 0)
        {
            AddonHelper.Callback(ContentsFinder, true, 3, 1U);
            agent->OpenRegularDuty(4);
            return false;
        }

        if (ContentsFinder->AtkValues[7].UInt == 0)
        {
            AddonHelper.Callback(ContentsFinder, true, 12, 0);
            return true;
        }
        return false;
    }

    public bool? CommenceDuty()
    {
        if (Service.ModuleManager.IsModuleEnabled(typeof(AutoCommenceDuty)))
        {
            TaskManager.InsertDelayNext(500);
            return true;
        }
        if (!TryGetAddonByName<AtkUnitBase>("ContentsFinderConfirm", out var addon) || !IsAddonAndNodesReady(addon)) return false;

        ClickContentsFinderConfirm.Using((nint)addon).Commence();;
        return true;
    }

    private static bool? ExitDuty()
    {
        if (Service.Condition[ConditionFlag.WaitingForDutyFinder] || 
            Service.Condition[ConditionFlag.InDutyQueue] || Flags.BetweenAreas()) return false;

        AbandonDuty(false);
        return true;
    }

    public override void Uninit()
    {
        Service.CommandManager.RemoveSubCommand("joinexitduty");

        base.Uninit();
    }
}
