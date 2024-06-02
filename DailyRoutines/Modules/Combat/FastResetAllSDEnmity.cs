using System.Linq;
using DailyRoutines.Managers;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("FastResetAllSDEnmityTitle", "FastResetAllSDEnmityDescription", ModuleCategories.战斗)]
public class FastResetAllSDEnmity : DailyModuleBase
{
    private const string Command = "resetallsd";
    private static bool IsAddCommand = true;

    public override void Init()
    {
        AddConfig(nameof(IsAddCommand), true);
        IsAddCommand = GetConfig<bool>(nameof(IsAddCommand));

        Service.ExecuteCommandManager.Register(OnResetStrikingDummies);
        if (IsAddCommand)
        {
            Service.CommandManager.AddSubCommand(Command, new CommandInfo(OnCommand)
            {
                HelpMessage = Service.Lang.GetText("FastResetAllSDEnmity-CommandHelp"),
            });
        }
    }

    public override void ConfigUI()
    {
        if (ImGui.Checkbox(
                $"{Service.Lang.GetText("FastResetAllSDEnmity-AddCommand", Command)}: {Service.Lang.GetText("FastResetAllSDEnmity-CommandHelp")}",
                ref IsAddCommand))
        {
            UpdateConfig(nameof(IsAddCommand), IsAddCommand);
            if (IsAddCommand)
            {
                Service.CommandManager.AddSubCommand(Command, new CommandInfo(OnCommand)
                {
                    HelpMessage = Service.Lang.GetText("FastResetAllSDEnmity-CommandHelp"),
                });
            }
            else
                Service.CommandManager.RemoveSubCommand(Command);
        }
    }

    private static void OnCommand(string command, string arguments) { ResetAllStrikingDummies(); }

    public static void OnResetStrikingDummies(int command, int objectID, int param2, int param3, int param4)
    {
        if (command != 319) return;

        ResetAllStrikingDummies();
    }

    private static void ResetAllStrikingDummies()
    {
        foreach (var strikingDummy in Service.ObjectTable.Where(x => x.ObjectKind == ObjectKind.BattleNpc &&
                                                                     x.TargetObject != null &&
                                                                     (x as BattleChara).NameId == 541))
            Service.ExecuteCommandManager.ExecuteCommand(319, (int)strikingDummy.ObjectId);
    }

    public override void Uninit()
    {
        Service.ExecuteCommandManager.Unregister(OnResetStrikingDummies);
        Service.CommandManager.RemoveSubCommand(Command);

        base.Uninit();
    }
}
