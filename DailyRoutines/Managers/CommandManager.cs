using System.Collections.Generic;
using System.Text;
using DailyRoutines.Windows;
using Dalamud.Game.Command;

namespace DailyRoutines.Managers;

public static class CommandManager
{
    public const string CommandPDR = "/pdr";
    private static readonly Dictionary<string, CommandInfo> AddedCommands = [];
    private static readonly Dictionary<string, CommandInfo> SubPDRArgs = [];

    public static void Init()
    {
        AddSubCommand("search", new CommandInfo(OnSubSearch) { HelpMessage = Service.Lang.GetText("CommandHelp-Search"), ShowInHelp = true });
        RefreshCommandDetails();
    }

    private static void RefreshCommandDetails()
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.Append(Service.Lang.GetText("CommandHelp"));
        foreach (var (command, commandInfo) in SubPDRArgs)
        {
            if (!commandInfo.ShowInHelp) continue;

            stringBuilder.Append($"\n{CommandPDR} {command} → {commandInfo.HelpMessage}");
        }

        RemoveCommand(CommandPDR);
        AddCommand(CommandPDR, new CommandInfo(OnCommandPDR) { HelpMessage = stringBuilder.ToString() }, true);
    }

    /// <summary>
    /// 添加一个独立于 /pdr 的命令
    /// </summary>
    /// <param name="command">要添加的命令, 需要附带斜杠</param>
    /// <param name="commandInfo"></param>
    /// <param name="isForceToAdd">如果已有同名命令, 是否强制覆盖添加</param>
    /// <returns></returns>
    public static bool AddCommand(string command, CommandInfo commandInfo, bool isForceToAdd = false)
    {
        switch (Service.Command.Commands.ContainsKey(command))
        {
            case true when !isForceToAdd:
                return false;
            case true when isForceToAdd:
                Service.Command.RemoveHandler(command);
                break;
        }

        Service.Command.AddHandler(command, commandInfo);
        AddedCommands.Add(command, commandInfo);

        return true;
    }

    /// <summary>
    /// 移除已添加的命令
    /// </summary>
    /// <param name="command"></param>
    /// <returns></returns>
    public static bool RemoveCommand(string command)
    {
        if (Service.Command.Commands.ContainsKey(command))
        {
            Service.Command.RemoveHandler(command);

            AddedCommands.Remove(command);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 添加一个依附于 /pdr 的子命令
    /// </summary>
    /// <param name="args">子命令触发参数, 不需要斜杠</param>
    /// <param name="commandInfo">你可以控制 ShowInHelp 属性来控制是否在详情页显示命令</param>
    /// <param name="isForceToAdd">如果已有同名命令, 是否强制覆盖添加</param>
    /// <returns></returns>
    public static bool AddSubCommand(string args, CommandInfo commandInfo, bool isForceToAdd = false)
    {
        switch (SubPDRArgs.ContainsKey(args))
        {
            case true when !isForceToAdd:
                return false;
            case true when isForceToAdd:
                SubPDRArgs.Remove(args);
                break;
        }

        SubPDRArgs.Add(args, commandInfo);
        RefreshCommandDetails();
        return true;
    }

    /// <summary>
    /// 删除一个依附于 /pdr 的子命令
    /// </summary>
    /// <param name="args"></param>
    /// <returns></returns>
    public static bool RemoveSubCommand(string args)
    {
        if (SubPDRArgs.Remove(args))
        {
            RefreshCommandDetails();
            return true;
        }

        return false;
    }

    private static void OnCommandPDR(string command, string args)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            Main.SearchString = string.Empty;
            P.Main.IsOpen ^= true;
        }

        var spiltedArgs = args.Split(' ', 2);
        if (SubPDRArgs.TryGetValue(spiltedArgs[0], out var commandInfo))
            commandInfo.Handler(spiltedArgs[0], spiltedArgs.Length > 1 ? spiltedArgs[1] : "");
    }

    private static void OnSubSearch(string command, string args)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            OnCommandPDR("", "");
            return;
        }
        Main.SearchString = args;
        P.Main.IsOpen ^= true;
    }

    public static void Uninit()
    {
        foreach (var command in AddedCommands.Keys)
            RemoveCommand(command);
        SubPDRArgs.Clear();
    }
}
