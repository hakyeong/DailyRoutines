using ClickLib;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.UI;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoRetainersDispatchTitle", "AutoRetainersDispatchDescription", ModuleCategories.Retainer)]
public class AutoRetainersDispatch : DailyModuleBase
{
    public override void Init()
    {
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = false };
    }

    public override void ConfigUI()
    {
        PreviewImageWithHelpText(Service.Lang.GetText("AutoRetainersDispatch-WhatIsTheList"), 
                                 "https://mirror.ghproxy.com/https://raw.githubusercontent.com/AtmoOmen/DailyRoutines/main/imgs/AutoRetainersDispatch-1.png", new(582, 325));

        if (ImGui.Button(Service.Lang.GetText("Start"))) EnqueueDispatch();

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("Stop"))) TaskManager.Abort();
    }

    private unsafe void EnqueueDispatch()
    {
        var addon = (AddonSelectString*)Service.Gui.GetAddonByName("SelectString");
        if (addon == null) return;

        var entryCount = addon->PopupMenu.PopupMenu.EntryCount;
        if (entryCount - 1 <= 0) return;

        for (var i = 0; i < entryCount - 1; i++)
        {
            var tempI = i;
            TaskManager.Enqueue(() => Click.TrySendClick($"select_string{tempI + 1}"));
            TaskManager.DelayNext(20);
            TaskManager.Enqueue(() => Click.TrySendClick("select_yes"));

            TaskManager.DelayNext(100);
        }
    }
}
