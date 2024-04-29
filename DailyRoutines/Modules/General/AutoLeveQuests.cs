using System;
using System.Collections.Generic;
using System.Linq;
using ClickLib;
using DailyRoutines.Helpers;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Interface.Utility;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoLeveQuestsTitle", "AutoLeveQuestsDescription", ModuleCategories.General)]
public unsafe class AutoLeveQuests : DailyModuleBase
{
    private static AtkUnitBase* SelectString => (AtkUnitBase*)Service.Gui.GetAddonByName("SelectString");
    private static AtkUnitBase* SelectIconString => (AtkUnitBase*)Service.Gui.GetAddonByName("SelectIconString");
    private static AtkUnitBase* GuildLeve => (AtkUnitBase*)Service.Gui.GetAddonByName("GuildLeve");


    private const string LeveAllowanceSig = "88 05 ?? ?? ?? ?? 0F B7 41 06";
    private static Dictionary<uint, Leve> LeveQuests = [];
    internal static Leve? SelectedLeve;
    private static uint LeveMeteDataId;
    private static uint LeveReceiverDataId;
    private static string SearchString = string.Empty;

    private static int ConfigOperationDelay;

    public override void Init()
    {
        Service.ClientState.TerritoryChanged += OnZoneChanged;
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 30000, ShowDebug = false };

        AddConfig("OperationDelay", 0);
        ConfigOperationDelay = GetConfig<int>("OperationDelay");
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "SelectYesno", AlwaysYes);
    }

    public override void ConfigUI()
    {
        ImGui.BeginDisabled(TaskManager.IsBusy);
        ImGui.SetNextItemWidth(80f * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt(Service.Lang.GetText("AutoLeveQuests-OperationDelay"), ref ConfigOperationDelay, 0, 0,
                           ImGuiInputTextFlags.EnterReturnsTrue))
        {
            ConfigOperationDelay = Math.Max(0, ConfigOperationDelay);
            UpdateConfig("OperationDelay", ConfigOperationDelay);
        }

        ImGui.SameLine();
        ImGuiOm.HelpMarker(Service.Lang.GetText("AutoLeveQuests-OperationDelayHelp"));

        ImGui.AlignTextToFramePadding();
        ImGui.Text($"{Service.Lang.GetText("AutoLeveQuests-SelectedLeve")}");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(300f * ImGuiHelpers.GlobalScale);
        if (ImGui.BeginCombo("##SelectedLeve", SelectedLeve == null ? "" : $"{SelectedLeve.Name.RawString}",
                             ImGuiComboFlags.HeightLarge))
        {
            if (ImGui.Button(Service.Lang.GetText("AutoLeveQuests-GetAreaLeveData"))) GetMapLeveQuests();

            ImGui.SetNextItemWidth(-1f);
            ImGui.SameLine();
            ImGui.InputText("##AutoLeveQuests-SearchLeveQuest", ref SearchString, 100);

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

        ImGui.SameLine();
        ImGui.BeginDisabled(SelectedLeve == null || LeveMeteDataId == LeveReceiverDataId || LeveMeteDataId == 0 ||
                            LeveReceiverDataId == 0);
        if (ImGui.Button(Service.Lang.GetText("Start")))
            EnqueueARound();

        ImGui.EndDisabled();
        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("Stop")))
            TaskManager.Abort();

        ImGui.BeginDisabled(TaskManager.IsBusy);
        if (ImGui.Button(Service.Lang.GetText("AutoLeveQuests-ObtainLevemeteID")))
            LeveMeteDataId = GetCurrentTargetDataID();

        ImGui.SameLine();
        ImGui.Text(LeveMeteDataId.ToString());

        ImGui.SameLine();
        ImGui.Spacing();

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("AutoLeveQuests-ObtainLeveClientID")))
            LeveReceiverDataId = GetCurrentTargetDataID();

        ImGui.SameLine();
        ImGui.Text(LeveReceiverDataId.ToString());

        ImGui.EndDisabled();
    }

    public void EnqueueARound()
    {
        var allowances = *(byte*)Service.SigScanner.GetStaticAddressFromSig(LeveAllowanceSig);
        if (SelectedLeve == null || allowances <= 0)
        {
            TaskManager.Abort();
            return;
        }

        // 与理符发行人交互
        TaskManager.Enqueue(InteractWithMete);
        // 点击对应理符类别
        if (ConfigOperationDelay > 0) TaskManager.DelayNext(ConfigOperationDelay);
        TaskManager.Enqueue(ClickLeveGenre);
        // 接取对应理符任务
        TaskManager.Enqueue(AcceptLeveQuest);
        // 退出理符任务界面
        TaskManager.Enqueue(ExitLeveInterface);
        // 与理符委托人交互
        TaskManager.Enqueue(InteractWithReceiver);
        // 检查是否有多个理符待提交
        if (ConfigOperationDelay > 0) TaskManager.DelayNext(ConfigOperationDelay);
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
        if (TryFindObjectToInteractWith(LeveMeteDataId, out var foundObject))
        {
            TargetSystem.Instance()->Target = foundObject;
            TargetSystem.Instance()->InteractWithObject(foundObject);
            return true;
        }

        return false;
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

        return true;
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
        if (TryFindObjectToInteractWith(LeveReceiverDataId, out var foundObject))
        {
            TargetSystem.Instance()->Target = foundObject;
            TargetSystem.Instance()->InteractWithObject(foundObject);
            return true;
        }

        return false;
    }

    private bool? CheckIfMultipleLevesToSubmit()
    {
        var levesSpan = QuestManager.Instance()->LeveQuestsSpan;
        var qualifiedCount = 0;

        foreach (var leve in levesSpan)
            if (LeveQuests.ContainsKey(leve.LeveId)) // 判断是否为当前地图的理符
                qualifiedCount++;

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


    private static bool TryFindObjectToInteractWith(uint dataId, out GameObject* foundObject)
    {
        foundObject = null;

        var objAddress = Service.ObjectTable
                                .FirstOrDefault(x => x.DataId == dataId && x.IsTargetable).Address;
        if (objAddress != nint.Zero)
        {
            foundObject = (GameObject*)objAddress;
            return true;
        }

        return false;
    }

    private static void GetMapLeveQuests()
    {
        var currentTerritoryPlaceNameId =
            LuminaCache.GetRow<TerritoryType>(Service.ClientState.TerritoryType).PlaceName.Row;

        LeveQuests = LuminaCache.Get<Leve>()
                                .Where(x => x.PlaceNameIssued.Row == currentTerritoryPlaceNameId &&
                                            !string.IsNullOrEmpty(x.Name.RawString) &&
                                            x.ClassJobCategory.Row is >= 9 and <= 16 or 19)
                                .ToDictionary(x => x.RowId, x => x);

        Service.Log.Debug($"成功获取了 {LeveQuests.Count} 个理符任务");
    }

    private static uint GetCurrentTargetDataID()
    {
        var currentTarget = Service.Target.Target;
        return currentTarget == null ? 0 : currentTarget.DataId;
    }

    private static void OnZoneChanged(ushort zone)
    {
        LeveQuests.Clear();
        SelectedLeve = null;
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

        base.Uninit();
    }
}
