using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using ClickLib;
using DailyRoutines.Helpers;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Hooking;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Utility.Signatures;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Application.Network.WorkDefinitions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;

namespace DailyRoutines.Modules;

[PrecedingModule([typeof(AutoRequestItemSubmit), typeof(AutoTalkSkip), typeof(AutoQuestComplete)])]
[ModuleDescription("AutoLeveQuestsTitle", "AutoLeveQuestsDescription", ModuleCategories.一般)]
public unsafe class AutoLeveQuests : DailyModuleBase
{
    private static AtkUnitBase* SelectString => (AtkUnitBase*)Service.Gui.GetAddonByName("SelectString");
    private static AtkUnitBase* SelectIconString => (AtkUnitBase*)Service.Gui.GetAddonByName("SelectIconString");
    private static AtkUnitBase* GuildLeve => (AtkUnitBase*)Service.Gui.GetAddonByName("GuildLeve");

    private delegate byte IsTargetableDelegate(GameObject* gameObj);
    [Signature("40 53 48 83 EC 20 F3 0F 10 89 ?? ?? ?? ?? 0F 57 C0 0F 2E C8 48 8B D9 7A 0A",
               DetourName = nameof(IsTargetableDetour))]
    private static Hook<IsTargetableDelegate>? IsTargetableHook;

    [Signature("88 05 ?? ?? ?? ?? 0F B7 41 06", ScanType = ScanType.StaticAddress)]
    private static byte LeveAllowances;

    private static Dictionary<uint, Leve> LeveQuests = [];

    private static Leve? SelectedLeve;
    private static GameObject* LeveMete;
    private static GameObject* LeveReceiver;
    private static string SearchString = string.Empty;

    private static float RandomRotation;
    private static int OperationDelay;

    public override void Init()
    {
        Service.Hook.InitializeFromAttributes(this);
        Service.ClientState.TerritoryChanged += OnZoneChanged;
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 30000, ShowDebug = false };

        AddConfig("OperationDelay", 0);
        OperationDelay = GetConfig<int>("OperationDelay");

        RandomRotation = new Random().Next(0, 360);
    }

    public override void ConfigUI()
    {
        var isLeveNPCNotQualified = LeveMete == LeveReceiver || LeveMete == null || LeveReceiver == null;
        var isLeveEmpty = SelectedLeve == null;

        ImGui.BeginDisabled(TaskManager.IsBusy);
        ImGui.SetNextItemWidth(80f * ImGuiHelpers.GlobalScale);
        ImGui.InputInt(Service.Lang.GetText("AutoLeveQuests-OperationDelay"), ref OperationDelay, 0, 0);

        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            OperationDelay = Math.Max(0, OperationDelay);
            UpdateConfig("OperationDelay", OperationDelay);
        }

        ImGui.SameLine();
        ImGuiOm.HelpMarker(Service.Lang.GetText("AutoLeveQuests-OperationDelayHelp"));

        if (isLeveEmpty)
            ImGuiHelpers.ScaledDummy(6f);

        ImGui.BeginGroup();
        ImGui.AlignTextToFramePadding();
        ImGui.Text($"{Service.Lang.GetText("AutoLeveQuests-SelectedLeve")}");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(300f * ImGuiHelpers.GlobalScale);
        if (ImGui.BeginCombo("##SelectedLeve", isLeveEmpty ? "" : $"{SelectedLeve.Name.RawString}",
                             ImGuiComboFlags.HeightLarge))
        {
            if (ImGui.Button(Service.Lang.GetText("AutoLeveQuests-GetAreaLeveData"))) GetMapLeveQuests();

            ImGui.SetNextItemWidth(-1f);
            ImGui.SameLine();
            ImGui.InputTextWithHint("##AutoLeveQuests-SearchLeveQuest", Service.Lang.GetText("PleaseSearch"),
                                    ref SearchString, 100);

            ImGui.Separator();
            if (LeveQuests.Count != 0)
            {
                foreach (var leve in LeveQuests)
                {
                    if (!string.IsNullOrWhiteSpace(SearchString) &&
                        !leve.Value.Name.RawString.Contains(SearchString) &&
                        !leve.Value.ClassJobCategory.Value.Name.RawString.Contains(SearchString) &&
                        !leve.Value.RowId.ToString().Contains(SearchString))
                        continue;

                    if (ImGui.Selectable(
                            $"{leve.Value.ClassJobCategory.Value.Name.RawString}{leve.Value.Name.RawString[leve.Value.Name.RawString.IndexOf('：')..]} ({leve.Value.RowId})"))
                        SelectedLeve = leve.Value;

                    ImGui.Separator();
                }
            }

            ImGui.EndCombo();
        }

        ImGui.EndGroup();

        if (isLeveEmpty)
        {
            ImGui.GetWindowDrawList().AddRect(ImGui.GetItemRectMin() - ImGuiHelpers.ScaledVector2(5f), ImGui.GetItemRectMax() + ImGuiHelpers.ScaledVector2(5f), ImGui.ColorConvertFloat4ToU32(ImGuiColors.DalamudRed), 2f, ImDrawFlags.RoundCornersAll, 3f);

            ImGui.SameLine();
            ImGui.Spacing();
        }

        ImGui.SameLine();
        ImGui.BeginDisabled(isLeveEmpty || isLeveNPCNotQualified);
        if (ImGui.Button(Service.Lang.GetText("Start")))
        {
            Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "SelectYesno", AlwaysYes);
            Service.ExecuteCommandManager.Register(OnPreExecuteCommand);
            IsTargetableHook?.Enable();
            EnqueueARound();
        }

        ImGui.EndDisabled();
        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("Stop")))
        {
            TaskManager.Abort();
            Service.AddonLifecycle.UnregisterListener(AlwaysYes);
            IsTargetableHook?.Disable();
            Service.ExecuteCommandManager.Unregister(OnPreExecuteCommand);
        }

        if (isLeveEmpty)
            ImGuiHelpers.ScaledDummy(6f);

        if (isLeveNPCNotQualified)
            ImGuiHelpers.ScaledDummy(6f);

        ImGui.BeginGroup();
        ImGui.BeginDisabled(TaskManager.IsBusy);
        if (ImGui.Button(Service.Lang.GetText("AutoLeveQuests-ObtainLevemete")))
            LeveMete = TargetSystem.Instance()->Target;

        ImGui.SameLine();
        ImGui.Text(LeveMete == null ? Service.Lang.GetText("AutoLeveQuests-ObtainHelp") : 
                       $"{Marshal.PtrToStringUTF8((nint)LeveMete->Name)} ({LeveMete->DataID})");

        ImGui.SameLine();
        ImGui.Spacing();

        if (ImGui.Button(Service.Lang.GetText("AutoLeveQuests-ObtainLeveClient")))
            LeveReceiver = TargetSystem.Instance()->Target;

        ImGui.SameLine();
        ImGui.Text(LeveReceiver == null ? Service.Lang.GetText("AutoLeveQuests-ObtainHelp") : 
                       $"{Marshal.PtrToStringUTF8((nint)LeveReceiver->Name)} ({LeveReceiver->DataID})");
        ImGui.EndGroup();

        if (isLeveNPCNotQualified)
        {
            ImGui.GetWindowDrawList().AddRect(ImGui.GetItemRectMin() - ImGuiHelpers.ScaledVector2(5f), ImGui.GetItemRectMax() + ImGuiHelpers.ScaledVector2(5f), ImGui.ColorConvertFloat4ToU32(ImGuiColors.DalamudYellow), 2f, ImDrawFlags.RoundCornersAll, 3f);

            ImGuiHelpers.ScaledDummy(6f);
        }

        ImGui.EndDisabled();
    }

    public void EnqueueARound()
    {
        if (SelectedLeve == null || LeveAllowances <= 0)
        {
            TaskManager.Abort();
            Service.AddonLifecycle.UnregisterListener(AlwaysYes);
            IsTargetableHook?.Disable();
            Service.ExecuteCommandManager.Unregister(OnPreExecuteCommand);
            return;
        }

        // 与理符发行人交互
        TaskManager.Enqueue(InteractWithMete);
        // 点击对应理符类别
        if (OperationDelay > 0) TaskManager.DelayNext(OperationDelay);
        TaskManager.Enqueue(ClickLeveGenre);
        // 接取对应理符任务
        TaskManager.Enqueue(AcceptLeveQuest);
        // 退出理符任务界面
        TaskManager.Enqueue(ExitLeveInterface);
        // 与理符委托人交互
        TaskManager.Enqueue(InteractWithReceiver);
        // 检查是否有多个理符待提交
        if (OperationDelay > 0) TaskManager.DelayNext(OperationDelay);
        TaskManager.Enqueue(CheckIfMultipleLevesToSubmit);
    }

    private static bool? InteractWithMete()
    {
        var continueToSubmit = LuminaCache.GetRow<CraftLeveClient>(1).Text.RawString;
        if (SelectString != null && IsAddonAndNodesReady(SelectString) &&
            TryScanSelectStringText(SelectString, continueToSubmit, out _))
        {
            ClickHelper.SelectString(continueToSubmit);
            return false;
        }

        if (IsOccupied()) return false;
        TargetSystem.Instance()->Target = LeveMete;
        TargetSystem.Instance()->InteractWithObject(LeveMete);
        return true;
    }

    private static bool? ClickLeveGenre()
    {
        if (SelectString == null || !IsAddonAndNodesReady(SelectString)) return false;

        var fieldcraft = LuminaCache.GetRow<Addon>(595).Text.RawString;
        var tradecraft = LuminaCache.GetRow<Addon>(596).Text.RawString;
        if (!ClickHelper.SelectString(SelectedLeve.ClassJobCategory.Row is 19 ? fieldcraft : tradecraft))
            return false;

        return true;
    }

    private static bool? AcceptLeveQuest()
    {
        if (GuildLeve == null || !IsAddonAndNodesReady(GuildLeve)) return false;

        AgentHelper.SendEvent(AgentId.LeveQuest, 0, 3, SelectedLeve.RowId);
        
        return QuestManager.Instance()->LeveQuestsSpan.ToArray().Select(x => (LeveWork?)x).FirstOrDefault(x => x.HasValue && x.Value.LeveId == SelectedLeve.RowId) != null;
    }

    private static bool? ExitLeveInterface()
    {
        if (GuildLeve != null && IsAddonAndNodesReady(GuildLeve))
        {
            GuildLeve->FireCloseCallback();
            GuildLeve->Close(true);

            return false;
        }

        var cancel = LuminaCache.GetRow<Addon>(581).Text.RawString;
        if (SelectString != null && IsAddonAndNodesReady(SelectString))
        {
            if (!ClickHelper.SelectString(cancel)) return false;
            return true;
        }

        return false;
    }

    private static bool? InteractWithReceiver()
    {
        if (IsOccupied()) return false;
        TargetSystem.Instance()->Target = LeveReceiver;
        TargetSystem.Instance()->InteractWithObject(LeveReceiver);
        return true;
    }

    private bool? CheckIfMultipleLevesToSubmit()
    {
        var levesSpan = QuestManager.Instance()->LeveQuestsSpan;
        var qualifiedCount = 0;

        // 判断是否为当前地图的理符
        foreach (var leve in levesSpan)
            if (LeveQuests.ContainsKey(leve.LeveId)) qualifiedCount++;

        if (qualifiedCount > 1)
        {
            if (SelectIconString != null && IsAddonAndNodesReady(SelectIconString))
            {
                if (!ClickHelper.SelectIconString(SelectedLeve.Name.RawString)) return false;

                EnqueueARound();
                return true;
            }

            return false;
        }

        EnqueueARound();
        return true;
    }

    private static void GetMapLeveQuests()
    {
        var currentZonePlaceNameID = LuminaCache.GetRow<TerritoryType>(Service.ClientState.TerritoryType).PlaceName.Row;

        if (Service.ClientState.TerritoryType == 0 || currentZonePlaceNameID == 0)
        {
            Service.Chat.PrintError(Service.Lang.GetText("AutoLeveQuests-GetMapLevesFailed"), "Daily Routines");
            return;
        }

        LeveQuests = LuminaCache.Get<Leve>()
                                .Where(x => x.PlaceNameIssued.Row == currentZonePlaceNameID &&
                                            !string.IsNullOrEmpty(x.Name.RawString) &&
                                            x.ClassJobCategory.Row is (>= 9 and <= 16) or 19)
                                .ToDictionary(x => x.RowId, x => x);
    }

    private void OnPreExecuteCommand
        (ref int command, ref int param1, ref int param2, ref int param3, ref int param4)
    {
        if (command != 3 || !TaskManager.IsBusy) return;

        param1 = int.MinValue / 4;
    }

    private static byte IsTargetableDetour(GameObject* pTarget)
    {
        var isTargetable = IsTargetableHook.Original(pTarget);

        var localPlayer = (GameObject*)Service.ClientState.LocalPlayer.Address;
        localPlayer->Rotation = RandomRotation;
        localPlayer->Rotate(RandomRotation);

        return isTargetable;
    }

    private static void OnZoneChanged(ushort zone)
    {
        LeveQuests.Clear();
        SelectedLeve = null;
        LeveMete = null;
        LeveReceiver = null;
    }

    private void AlwaysYes(AddonEvent type, AddonArgs args)
    {
        if (!TaskManager.IsBusy) return;
        Click.SendClick("select_yes");
    }

    public override void Uninit()
    {
        Service.ClientState.TerritoryChanged -= OnZoneChanged;
        Service.AddonLifecycle.UnregisterListener(AlwaysYes);
        Service.ExecuteCommandManager.Unregister(OnPreExecuteCommand);

        base.Uninit();
    }
}
