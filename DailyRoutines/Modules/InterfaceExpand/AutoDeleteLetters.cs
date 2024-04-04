using System.Numerics;
using ClickLib;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using DailyRoutines.Windows;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Interface.Colors;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace DailyRoutines.Modules; 

[ModuleDescription("AutoDeleteLettersTitle", "AutoDeleteLettersDescription", ModuleCategories.InterfaceExpand)]
public unsafe class AutoDeleteLetters : DailyModuleBase
{
    public override void Init()
    {
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = false };
        Overlay ??= new Overlay(this);

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, "SelectYesno", AlwaysYes);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "LetterList", OnAddonLetterList);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "LetterList", OnAddonLetterList);
    }

    public override void OverlayUI()
    {
        var addon = (AtkUnitBase*)Service.Gui.GetAddonByName("LetterList");
        if (addon == null) return;

        var pos = new Vector2(addon->GetX() - ImGui.GetWindowSize().X, addon->GetY() + 6);
        ImGui.SetWindowPos(pos);

        ImGui.TextColored(ImGuiColors.DalamudYellow, Service.Lang.GetText("AutoDeleteLettersTitle"));

        ImGui.Separator();
        ImGui.BeginDisabled(TaskManager.IsBusy);
        if (ImGui.Button(Service.Lang.GetText("Start"))) TaskManager.Enqueue(RightClickLetter);
        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("Stop"))) TaskManager.Abort();
    }

    private void OnAddonLetterList(AddonEvent type, AddonArgs _)
    {
        Overlay.IsOpen = type switch
        {
            AddonEvent.PostSetup => true,
            AddonEvent.PreFinalize => false,
            _ => Overlay.IsOpen
        };
    }

    public bool? RightClickLetter()
    {
        if (TryGetAddonByName<AtkUnitBase>("LetterList", out var addon) && HelpersOm.IsAddonAndNodesReady(addon))
        {
            if (!int.TryParse(addon->GetTextNodeById(23)->NodeText.ExtractText().Split('/')[0],
                              out var currentLetters) || currentLetters == 0)
            {
                TaskManager.Abort();
                return true;
            }

            var pnrLetters = addon->GetNodeById(7)->GetComponent()->UldManager.NodeList[2]->IsVisible; // 特典与商城道具邮件

            AddonManager.Callback(addon, true, 0, pnrLetters ? 0 : 1, 0, 1); // 第二个 0 是索引

            TaskManager.DelayNext(100);
            TaskManager.Enqueue(ClickDeleteEntry);
            return true;
        }

        return false;
    }

    public bool? ClickDeleteEntry()
    {
        if (TryGetAddonByName<AtkUnitBase>("ContextMenu", out var addon) && HelpersOm.IsAddonAndNodesReady(addon))
        {
            if (!HelpersOm.TryScanContextMenuText(addon, "删除", out var index)) return false;

            AddonManager.Callback(addon, true, 0, index, 0, 0, 0);

            TaskManager.DelayNext(100);
            TaskManager.Enqueue(RightClickLetter);
            return true;
        }

        return false;
    }

    private void AlwaysYes(AddonEvent type, AddonArgs args)
    {
        if (!TaskManager.IsBusy) return;
        Click.SendClick("select_yes");
    }

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddonLetterList);
        Service.AddonLifecycle.UnregisterListener(AlwaysYes);

        base.Uninit();
    }
}
