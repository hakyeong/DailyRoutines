using System.Collections.Generic;
using System.Linq;
using DailyRoutines.Windows;
using Dalamud.Game.Command;

namespace DailyRoutines.Managers;

public static class CommandManager
{
    public const string CommandPDR = "/pdr";
    private static readonly HashSet<string> addedCommands = [];

    public static void Init()
    {
        AddCommand(CommandPDR, new CommandInfo(OnCommandPDR) { HelpMessage = Service.Lang.GetText("CommandHelp") }, true);
    }

    public static bool AddCommand(string command, CommandInfo commandInfo, bool isForceToAdd = false)
    {
        var addedCommand = Service.Command.Commands;
        var isDuplicate = addedCommand.Keys.Any(x => x == command);

        switch (isDuplicate)
        {
            case true when !isForceToAdd:
                return false;
            case true when isForceToAdd:
                Service.Command.RemoveHandler(command);
                break;
        }

        addedCommands.Add(command);
        Service.Command.AddHandler(command, commandInfo);
        return true;
    }

    public static bool RemoveCommand(string command)
    {
        if (Service.Command.Commands.Keys.All(x => x != command))
        {
            Service.Command.RemoveHandler(command);
            return true;
        }

        return false;
    }

    private static void OnCommandPDR(string command, string args)
    {
        if (!string.IsNullOrEmpty(args) && args != Main.SearchString)
        {
            Main.SearchString = args;
            P.Main.IsOpen = true;
        }
        else
        {
            if (string.IsNullOrEmpty(args))
                Main.SearchString = string.Empty;

            P.Main.IsOpen ^= true;
        }
    }

    public static void Uninit()
    {
        foreach (var command in addedCommands)
            RemoveCommand(command);
    }
}
