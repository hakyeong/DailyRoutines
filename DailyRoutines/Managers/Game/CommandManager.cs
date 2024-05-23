using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using DailyRoutines.Infos;
using DailyRoutines.Modules;
using DailyRoutines.Windows;
using Dalamud.Game.Command;

namespace DailyRoutines.Managers;

public class CommandManager : IDailyManager
{
    public const string CommandPDR = "/pdr";
    private static readonly Dictionary<string, CommandInfo> AddedCommands = [];
    private static readonly Dictionary<string, CommandInfo> SubPDRArgs = [];

    private void Init()
    {
        RefreshCommandDetails();
        AddSubCommand("search", new CommandInfo(OnSubSearch) { HelpMessage = Service.Lang.GetText("CommandHelp-Search") });
        AddSubCommand("load", new CommandInfo(OnSubLoad) { HelpMessage = Service.Lang.GetText("CommandHelp-Load") });
        AddSubCommand("unload", new CommandInfo(OnSubUnload) { HelpMessage = Service.Lang.GetText("CommandHelp-Unload") });
        AddSubCommand("debug", new CommandInfo(OnSubDebug) { ShowInHelp = false });
    }

    private void RefreshCommandDetails()
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
    public bool AddCommand(string command, CommandInfo commandInfo, bool isForceToAdd = false)
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
    public bool RemoveCommand(string command)
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
    public bool AddSubCommand(string args, CommandInfo commandInfo, bool isForceToAdd = false)
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
    public bool RemoveSubCommand(string args)
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
            WindowManager.Main.IsOpen ^= true;
        }

        var spiltedArgs = args.Split(' ', 2);
        if (string.IsNullOrWhiteSpace(spiltedArgs[0])) return;

        if (SubPDRArgs.TryGetValue(spiltedArgs[0], out var commandInfo))
            commandInfo.Handler(spiltedArgs[0], spiltedArgs.Length > 1 ? spiltedArgs[1] : "");
        else
            Service.Chat.PrintError($"“{spiltedArgs[0]}”出现问题：该命令不存在。");
    }

    private static void OnSubDebug(string command, string args)
    {
        WindowManager.Debug.IsOpen ^= true;
    }

    private static void OnSubSearch(string command, string args)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            OnCommandPDR("", "");
            return;
        }
        Main.SearchString = args;
        WindowManager.Main.IsOpen ^= true;
    }

    private static void OnSubLoad(string command, string args)
    {
        var moduleName = args.Trim();
        if (string.IsNullOrWhiteSpace(moduleName)) return;

        var moduleType = Assembly.GetExecutingAssembly()
                                 .GetTypes()
                                 .FirstOrDefault(t => typeof(DailyModuleBase).IsAssignableFrom(t) &&
                                                      t is { IsClass: true, IsAbstract: false } &&
                                                      string.Equals(t.Name, moduleName, StringComparison.OrdinalIgnoreCase));
        if (moduleType == null) return;

        var module = Service.ModuleManager.Modules[moduleType];
        Service.ModuleManager.Load(module, true);
    }

    private static void OnSubUnload(string command, string args)
    {
        var moduleName = args.Trim();
        if (string.IsNullOrWhiteSpace(moduleName)) return;

        var moduleType = Assembly.GetExecutingAssembly()
                                 .GetTypes()
                                 .FirstOrDefault(t => typeof(DailyModuleBase).IsAssignableFrom(t) &&
                                                      t is { IsClass: true, IsAbstract: false } &&
                                                      string.Equals(t.Name, moduleName, StringComparison.OrdinalIgnoreCase));
        if (moduleType == null) return;

        var module = Service.ModuleManager.Modules[moduleType];
        Service.ModuleManager.Unload(module, true);
    }

    private void Uninit()
    {
        foreach (var command in AddedCommands.Keys)
            RemoveCommand(command);
        SubPDRArgs.Clear();
    }
}
