using System;
using System.Collections.Generic;
using System.Linq;
using ClickLib;
using ClickLib.Bases;
using ClickLib.Clicks;
using DailyRoutines.Clicks;
using DailyRoutines.Infos;
using DailyRoutines.Manager;
using DailyRoutines.Managers;
using Dalamud.Game.AddonLifecycle;
using Dalamud.Game.ClientState.Conditions;
using ECommons.Automation;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoLeveQuestsTitle", "AutoLeveQuestsDescription", ModuleCategories.General)]
public class AutoLeveQuests : IDailyModule
{
    public bool Initialized { get; set; }

    private static Dictionary<uint, (string, uint)> LeveQuests = new();
    private static readonly HashSet<uint> QualifiedLeveCategories = new() { 9, 10, 11, 12, 13, 14, 15, 16 };

    private static (uint, string, uint)? SelectedLeve; // Leve ID - Leve Name - Leve Job Category

    private static TaskManager? TaskManager;

    private static uint LeveMeteDataId;
    private static uint LeveReceiverDataId;
    private static int Allowances;
    private static string SearchString = string.Empty;

    private static bool IsOnProcessing;

    public void Init()
    {
        Service.ClientState.TerritoryChanged += OnZoneChanged;
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 30000, ShowDebug = true };

        Initialized = true;
    }
    public void UI()
    {
        ImGui.BeginDisabled(IsOnProcessing);
        ImGui.AlignTextToFramePadding();
        ImGui.Text($"{Service.Lang.GetText("AutoLeveQuests-SelectedLeve")}");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(400f);
        if (ImGui.BeginCombo("##SelectedLeve",
                             SelectedLeve == null ? "" : $"{SelectedLeve.Value.Item1} | {SelectedLeve.Value.Item2}"))
        {
            if (ImGui.Button(Service.Lang.GetText("AutoLeveQuests-GetAreaLeveData"))) GetRecentLeveQuests();

            ImGui.SetNextItemWidth(-1f);
            ImGui.SameLine();
            ImGui.InputText("##AutoLeveQuests-SearchLeveQuest", ref SearchString, 100);

            ImGui.Separator();
            if (LeveQuests.Any())
            {
                foreach (var leveToSelect in LeveQuests)
                {
                    if (!string.IsNullOrEmpty(SearchString) && !leveToSelect.Value.Item1.Contains(SearchString, StringComparison.OrdinalIgnoreCase) && !leveToSelect.Key.ToString().Contains(SearchString, StringComparison.OrdinalIgnoreCase)) continue;
                    if (ImGui.Selectable($"{leveToSelect.Key} | {leveToSelect.Value.Item1}"))
                        SelectedLeve = (leveToSelect.Key, leveToSelect.Value.Item1, leveToSelect.Value.Item2);
                    if (SelectedLeve != null && ImGui.IsWindowAppearing() && SelectedLeve.Value.Item1 == leveToSelect.Key)
                        ImGui.SetScrollHereY();
                }
            }

            ImGui.EndCombo();
        }

        ImGui.SameLine();
        ImGui.BeginDisabled(!ModuleManager.Modules.FirstOrDefault(c => c.GetType() == typeof(AutoRequestItemSubmit)).Initialized || SelectedLeve == null || LeveMeteDataId == LeveReceiverDataId || LeveMeteDataId == 0 ||
                                                         LeveReceiverDataId == 0);
        if (ImGui.Button(Service.Lang.GetText("AutoLeveQuests-Start")))
        {
            IsOnProcessing = true;
            Service.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, "Talk", SkipTalk);
            Service.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, "SelectYesno", AlwaysYes);
            Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "JournalResult", OnAddonJournalResult);

            TaskManager.Enqueue(InteractWithMete);
        }

        ImGui.EndDisabled();
        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("AutoLeveQuests-Stop"))) EndProcessHandler();

        ImGui.BeginDisabled(IsOnProcessing);
        if (ImGui.Button(Service.Lang.GetText("AutoLeveQuests-ObtainLevemeteID"))) GetCurrentTargetDataID(out LeveMeteDataId);

        ImGui.SameLine();
        ImGui.Text(LeveMeteDataId.ToString());

        ImGui.SameLine();
        ImGui.Spacing();

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("AutoLeveQuests-ObtainLeveClientID"))) GetCurrentTargetDataID(out LeveReceiverDataId);

        ImGui.SameLine();
        ImGui.Text(LeveReceiverDataId.ToString());

        ImGui.EndDisabled();
    }

    private static void EndProcessHandler()
    {
        TaskManager?.Abort();
        Service.AddonLifecycle.UnregisterListener(SkipTalk);
        Service.AddonLifecycle.UnregisterListener(AlwaysYes);
        Service.AddonLifecycle.UnregisterListener(OnAddonJournalResult);
        IsOnProcessing = false;
    }

    private static void OnZoneChanged(object? sender, ushort e) => LeveQuests.Clear();

    private static void SkipTalk(AddonEvent type, AddonArgs args)
    {
        if (EzThrottler.Throttle("AutoRetainerCollect-Talk", 100)) Click.SendClick("talk");
    }

    private static void AlwaysYes(AddonEvent type, AddonArgs args)
    {
        Click.TrySendClick("select_yes");
    }

    private static unsafe void OnAddonJournalResult(AddonEvent type, AddonArgs args)
    {
        if (TryGetAddonByName<AddonJournalResult>("JournalResult", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(&addon->AtkUnitBase))
        {
            var ui = &addon->AtkUnitBase;
            var handler = new ClickJournalResult();
            handler.Complete();
            ui->Close(true);
        }
    }

    private static void GetRecentLeveQuests()
    {
        var currentTerritoryPlaceNameId = Service.Data.GetExcelSheet<TerritoryType>()
                                                 .FirstOrDefault(y => y.RowId == Service.ClientState.TerritoryType)?
                                                 .PlaceName.RawRow.RowId;

        if (currentTerritoryPlaceNameId.HasValue)
        {
            LeveQuests = Service.Data.GetExcelSheet<Leve>()
                                .Where(x => !string.IsNullOrEmpty(x.Name.RawString) &&
                                            QualifiedLeveCategories.Contains(x.ClassJobCategory.RawRow.RowId) &&
                                            x.PlaceNameIssued.RawRow.RowId == currentTerritoryPlaceNameId.Value)
                                .ToDictionary(x => x.RowId, x => (x.Name.RawString, x.ClassJobCategory.RawRow.RowId));

            Service.Log.Debug($"Obtained {LeveQuests.Count} leve quests");
        }
    }

    private static void GetCurrentTargetDataID(out uint targetDataId)
    {
        var currentTarget = Service.Target.Target;
        targetDataId = currentTarget == null ? 0 : currentTarget.DataId;
    }

    private static unsafe bool? InteractWithMete()
    {
        // 防止 "要继续交货吗"
        if (TryGetAddonByName<AddonSelectString>("SelectString", out var addon) && IsAddonReady(&addon->AtkUnitBase))
        {
            var i = 1;
            for (; i < 8; i++)
            {
                var text =
                    addon->PopupMenu.PopupMenu.List->AtkComponentBase.UldManager.NodeList[i]->GetAsAtkComponentNode()->
                        Component->UldManager.NodeList[3]->GetAsAtkTextNode()->NodeText.ExtractText();
                if (text.Contains("结束")) break;
            }

            var handler = new ClickSelectString();
            handler.SelectItem((ushort)(i-1));
        }

        if (IsOccupied()) return false;
        if (FindObjectToInteractWith(LeveMeteDataId, out var foundObject))
        {
            TargetSystem.Instance()->InteractWithObject(foundObject);

            TaskManager.Enqueue(ClickCraftingLeve);
            return true;
        }

        return false;
    }

    private static unsafe bool FindObjectToInteractWith(uint dataId, out GameObject* foundObject)
    {
        foreach (var obj in Service.ObjectTable.Where(o => o.DataId == dataId))
            if (obj.IsTargetable)
            {
                foundObject = (GameObject*)obj.Address;
                return true;
            }
        foundObject = null;
        return false;
    }

    private static unsafe bool? ClickCraftingLeve()
    {
        if (TryGetAddonByName<AtkUnitBase>("SelectString", out var addon) && IsAddonReady(addon))
        {
            var handler = new ClickSelectString();
            handler.SelectItem2();

            TaskManager.Enqueue(ClickLeveQuest);

            return true;
        }

        return false;
    }

    private static unsafe bool? ClickLeveQuest()
    {
        if (SelectedLeve == null) return false;
        if (TryGetAddonByName<AddonGuildLeve>("GuildLeve", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(&addon->AtkUnitBase))
        {
            Allowances =
                int.TryParse(
                    addon->AtkComponentBase290->UldManager.NodeList[2]->GetAsAtkTextNode()->NodeText.ExtractText(),
                    out var result)
                    ? result
                    : 0;
            if (Allowances <= 0) EndProcessHandler();

            if (TryGetAddonByName<AddonJournalDetail>("JournalDetail", out var addon1) &&
                HelpersOm.IsAddonAndNodesReady(&addon1->AtkUnitBase))
            {
                var handler2 = new ClickJournalDetailDR();
                handler2.Accept((int)SelectedLeve.Value.Item1);

                TaskManager.Enqueue(ClickExit);

                return true;
            }
        }

        return false;
    }

    internal static unsafe bool? ClickExit()
    {
        if (TryGetAddonByName<AddonGuildLeve>("GuildLeve", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(&addon->AtkUnitBase))
        {
            var ui = &addon->AtkUnitBase;
            var handler = new ClickGuildLeveDR();
            handler.Exit();

            TaskManager.Enqueue(ClickSelectStringExit);

            ui->Close(true);

            return true;
        }

        return false;
    }

    private static unsafe bool? ClickSelectStringExit()
    {
        if (SelectedLeve == null) return false;
        if (TryGetAddonByName<AtkUnitBase>("SelectString", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(addon))
        {
            var i = 1;
            for (; i < 8; i++)
            {
                var text =
                    ((AddonSelectString*)addon)->PopupMenu.PopupMenu.List->AtkComponentBase.UldManager.NodeList[i]->GetAsAtkComponentNode()->
                        Component->UldManager.NodeList[3]->GetAsAtkTextNode()->NodeText.ExtractText();
                if (text.Contains("取消")) break;
            }

            var handler = new ClickSelectString();
            handler.SelectItem((ushort)(i - 1));

            TaskManager.Enqueue(InteractWithReceiver);

            addon->Close(true);

            return true;
        }

        return false;
    }

    private static unsafe bool? InteractWithReceiver()
    {
        if (IsOccupied()) return false;
        if (FindObjectToInteractWith(LeveReceiverDataId, out var foundObject))
        {
            TargetSystem.Instance()->InteractWithObject(foundObject);

            // 判断当前是否有多个待提交理符
            var levesSpan = QuestManager.Instance()->LeveQuestsSpan;
            var qualifiedCount = 0;

            for (var i = 0; i < levesSpan.Length; i++)
            {
                if (LeveQuests.ContainsKey(levesSpan[i].LeveId)) qualifiedCount++;
            }

            TaskManager.Enqueue(qualifiedCount > 1 ? ClickSelectQuest : InteractWithMete);

            return true;
        }

        return false;
    }

    private static unsafe bool? ClickSelectQuest()
    {
        if (SelectedLeve == null) return false;
        if (TryGetAddonByName<AddonSelectIconString>("SelectIconString", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(&addon->AtkUnitBase))
        {
            var i = 1;
            for (; i < 8; i++)
            {
                var text =
                    addon->PopupMenu.PopupMenu.List->AtkComponentBase.UldManager.NodeList[i]->GetAsAtkComponentNode()->
                        Component->UldManager.NodeList[4]->GetAsAtkTextNode()->NodeText.ExtractText();
                if (text == SelectedLeve.Value.Item2)
                {
                    break;
                }
            }

            var handler = new ClickSelectIconString();
            handler.SelectItem((ushort)(i - 1));

            TaskManager.Enqueue(InteractWithMete);
            return true;
        }

        return false;
    }

    public void Uninit()
    {
        Service.ClientState.TerritoryChanged -= OnZoneChanged;
        EndProcessHandler();

        Initialized = false;
    }
}
