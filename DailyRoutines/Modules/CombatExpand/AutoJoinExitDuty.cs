using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Runtime.InteropServices;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using Lumina.Excel.GeneratedSheets;
using TaskManager = ECommons.Automation.TaskManager;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoJoinExitDutyTitle", "AutoJoinExitDutyDescription", ModuleCategories.CombatExpand)]
public unsafe class AutoJoinExitDuty : DailyModuleBase
{
    private static AtkUnitBase* ContentsFinder => (AtkUnitBase*)Service.Gui.GetAddonByName("ContentsFinder");

    private const string AbandonDutySig = "E8 ?? ?? ?? ?? 48 8B 43 28 B1 01";
    private delegate void AbandonDutyDelagte(bool a1);
    private static AbandonDutyDelagte? AbandonDuty;
    private static string? ContentName;

    public override void Init()
    {
        AbandonDuty ??= Marshal.GetDelegateForFunctionPointer<AbandonDutyDelagte>(Service.SigScanner.ScanText(AbandonDutySig));
        ContentName ??= LuminaCache.GetRow<ContentFinderCondition>(4).Name.ExtractText();

        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 60000, ShowDebug = false };
        CommandManager.AddSubCommand("joinexitduty", 
                                     new CommandInfo(OnCommand) { HelpMessage = Service.Lang.GetText("AutoJoinExitDutyTitle"), ShowInHelp = true });
    }

    private void OnCommand(string command, string arguments)
    {
        if (Flags.BoundByDuty() || !UIState.IsInstanceContentUnlocked(4) ||
            Service.ClientState.LocalPlayer == null || Service.ClientState.LocalPlayer.ClassJob.Id is >= 8 and <= 18) return;
        EnqueueARound();
    }

    private void EnqueueARound()
    {
        TaskManager.Enqueue(OpenContentsFinder);
        TaskManager.Enqueue(SelectDuty);
        TaskManager.DelayNext(1000);
        TaskManager.Enqueue(ExitDuty);
    }

    private static bool? OpenContentsFinder()
    {
        if (ContentsFinder != null && IsAddonAndNodesReady(ContentsFinder)) return true;

        AgentModule.Instance()->GetAgentContentsFinder()->AgentInterface.Show();
        return true;
    }

    private static bool? SelectDuty()
    {
        if (ContentsFinder == null || !IsAddonAndNodesReady(ContentsFinder)) return false;

        var agent = AgentModule.Instance()->GetAgentContentsFinder();
        var instance = FFXIVClientStructs.FFXIV.Client.Game.UI.ContentsFinder.Instance();
        var addon = (AddonContentsFinder*)ContentsFinder;
        if (agent == null || instance == null) return false;
        if (!instance->IsExplorerMode)
        {
            instance->IsExplorerMode = true;
            return false;
        }

        var selectedDuty = MemoryHelper.ReadSeStringNullTerminated((nint)ContentsFinder->AtkValues[18].String).ExtractText();
        if (addon->ClearSelectionButton->IsEnabled && !string.IsNullOrWhiteSpace(selectedDuty) && selectedDuty != ContentName)
        {
            AddonManager.Callback(ContentsFinder, true, 12, 1);
            return false;
        }

        if (string.IsNullOrWhiteSpace(selectedDuty) || (!string.IsNullOrWhiteSpace(selectedDuty) && selectedDuty != ContentName))
        {
            AddonManager.Callback(ContentsFinder, true, 1, 1);
            AddonManager.Callback(ContentsFinder, true, 3, 1);
            agent->OpenRegularDuty(4);

            ContentsFinder->AtkValues[18].SetString(ContentName);
            ContentsFinder->OnRefresh(ContentsFinder->AtkValuesCount, ContentsFinder->AtkValues);
        }

        if (!string.IsNullOrWhiteSpace(selectedDuty))
        {
            AddonManager.Callback(ContentsFinder, true, 12, 0);
            return true;
        }
        return false;
    }

    private static bool? ExitDuty()
    {
        if (Service.Condition[ConditionFlag.WaitingForDutyFinder] || 
            Service.Condition[ConditionFlag.BoundToDuty97] || Flags.BetweenAreas()) return false;

        AbandonDuty(false);
        return true;
    }

    public override void Uninit()
    {
        CommandManager.RemoveSubCommand("joinexitduty");

        base.Uninit();
    }
}
