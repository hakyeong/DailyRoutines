using System;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Command;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace DailyRoutines.Modules;

[ModuleDescription("GlamourPlateApplyCommandTitle", "GlamourPlateApplyCommandDescription", ModuleCategories.系统)]
public unsafe class GlamourPlateApplyCommand : DailyModuleBase
{
    private const string Command = "gpapply";

    public override void Init()
    {
        Service.CommandManager.AddSubCommand(Command,
                                             new CommandInfo(OnCommand)
                                             {
                                                 HelpMessage = Service.Lang.GetText("GlamourPlateApplyCommand-CommandHelp"),
                                             });
    }

    private static void OnCommand(string command, string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments) ||
            !int.TryParse(arguments.Trim(), out var index) || index is < 1 or > 20) return;

        var mirageManager = MirageManager.Instance();
        if (!mirageManager->GlamourPlatesLoaded)
        {
            Service.ExecuteCommandManager.ExecuteCommand(ExecuteCommandFlag.RequestGlamourPlates);
            Service.Framework.RunOnTick(() => ApplyGlamourPlate(index), TimeSpan.FromMilliseconds(500));
            return;
        }

        ApplyGlamourPlate(index);
    }

    private static void ApplyGlamourPlate(int index)
    {
        Service.ExecuteCommandManager.ExecuteCommand(ExecuteCommandFlag.EnterGlamourPlateState, 1, 1);
        Service.ExecuteCommandManager.ExecuteCommand(ExecuteCommandFlag.ApplyGlamourPlate, index - 1);
        Service.ExecuteCommandManager.ExecuteCommand(ExecuteCommandFlag.EnterGlamourPlateState, 0, 1);
    }

    public override void Uninit() { Service.CommandManager.RemoveSubCommand(Command); }
}
