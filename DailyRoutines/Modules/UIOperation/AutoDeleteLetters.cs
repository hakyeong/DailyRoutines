using System.Numerics;
using ClickLib;
using DailyRoutines.Helpers;
using DailyRoutines.Infos;
using DailyRoutines.Infos.Clicks;
using DailyRoutines.Managers;
using DailyRoutines.Windows;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Interface.Colors;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoDeleteLettersTitle", "AutoDeleteLettersDescription", ModuleCategories.界面操作)]
public unsafe class AutoDeleteLetters : DailyModuleBase
{
    public override void Init()
    {
        TaskHelper ??= new TaskHelper();
        Overlay ??= new Overlay(this);

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "SelectYesno", AlwaysYes);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "LetterList", OnAddonLetterList);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "LetterList", OnAddonLetterList);

        if (AddonState.LetterList != null) OnAddonLetterList(AddonEvent.PostSetup, null);
    }

    public override void OverlayUI()
    {
        var addon = AddonState.LetterList;
        if (addon == null) return;

        var pos = new Vector2(addon->GetX() - ImGui.GetWindowSize().X, addon->GetY() + 6);
        ImGui.SetWindowPos(pos);

        ImGui.TextColored(ImGuiColors.DalamudYellow, Service.Lang.GetText("AutoDeleteLettersTitle"));

        ImGui.Separator();

        ImGui.BeginDisabled(TaskHelper.IsBusy);
        if (ImGui.Button(Service.Lang.GetText("Start"))) TaskHelper.Enqueue(RightClickLetter);
        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("Stop"))) TaskHelper.Abort();
    }

    public bool? RightClickLetter()
    {
        var addon = AddonState.LetterList;
        if (addon == null || !IsAddonAndNodesReady(addon)) return false;

        var infoProxy = InfoProxyLetter.Instance();
        var category = CurrentlySelectedCategory();
        if (category == -1)
        {
            TaskHelper.Abort();
            return true;
        }

        var letterAmount = category switch
        {
            0 => infoProxy->NumLettersFromFriends,
            1 => infoProxy->NumLettersFromPurchases,
            2 => infoProxy->NumLettersFromGameMasters,
            _ => 0,
        };

        if (letterAmount == 0)
        {
            TaskHelper.Abort();
            return true;
        }

        Callback(addon, true, 0, category == 1 ? 0 : 1, 0, 1); // 第二个 0 是索引

        TaskHelper.DelayNext("Delay_RightClickLetter", 100);
        TaskHelper.Enqueue(ClickDeleteEntry);
        return true;
    }

    public bool? ClickDeleteEntry()
    {
        var addon = AddonState.ContextMenu;
        if (addon == null || !IsAddonAndNodesReady(addon)) return false;

        if (!ClickHelper.ContextMenu(LuminaCache.GetRow<Addon>(431).Text.RawString)) return false;

        TaskHelper.DelayNext("Delay_ClickDelete", 100);
        TaskHelper.Enqueue(RightClickLetter);
        return true;

    }

    private void AlwaysYes(AddonEvent type, AddonArgs args)
    {
        if (!TaskHelper.IsBusy) return;
        Click.SendClick("select_yes");
    }

    private void OnAddonLetterList(AddonEvent type, AddonArgs? _)
    {
        Overlay.IsOpen = type switch
        {
            AddonEvent.PostSetup => true,
            AddonEvent.PreFinalize => false,
            _ => Overlay.IsOpen,
        };
    }

    private static int CurrentlySelectedCategory()
    {
        var addon = AddonState.LetterList;
        if (addon == null) return -1;

        for (var i = 6U; i < 9U; i++)
        {
            var buttonNode = addon->GetButtonNodeById(i);
            if (buttonNode == null) continue;

            if ((buttonNode->Flags & 0x40000) != 0) return (int)(i - 6);
        }

        return -1;
    }

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddonLetterList);
        Service.AddonLifecycle.UnregisterListener(AlwaysYes);

        base.Uninit();
    }
}
