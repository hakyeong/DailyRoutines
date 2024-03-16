using System.Collections.Generic;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using ECommons.Automation;
using ECommons.GameFunctions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoSummonPetTitle", "AutoSummonPetDescription", ModuleCategories.Combat)]
public class AutoSummonPet : DailyModuleBase
{
    private static readonly Dictionary<uint, uint> SummonActions = new()
    {
        // 学者
        { 28, 17215 },
        // 秘术师 / 召唤师
        { 26, 25798 },
        { 27, 25798 }
    };

    public override void Init()
    {
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 30000, ShowDebug = false };

        Service.ClientState.TerritoryChanged += OnZoneChanged;
        Service.DutyState.DutyRecommenced += OnDutyRecommenced;
    }

    // 重新挑战
    private void OnDutyRecommenced(object? sender, ushort e)
    {
        TaskManager.Abort();
        TaskManager.Enqueue(CheckCurrentJob);
    }

    // 进入副本
    private void OnZoneChanged(ushort zone)
    {
        if (!Service.PresetData.Contents.ContainsKey(zone) || Service.ClientState.IsPvP) return;
        TaskManager.Abort();
        TaskManager.Enqueue(CheckCurrentJob);
    }

    private static unsafe bool? CheckCurrentJob()
    {
        if (TryGetAddonByName<AtkUnitBase>("NowLoading", out var addon) && HelpersOm.IsAddonAndNodesReady(addon))
            return false;

        var player = Service.ClientState.LocalPlayer;
        if (player == null || player.ClassJob.Id == 0) return false;

        var job = player.ClassJob.Id;
        if (!SummonActions.TryGetValue(job, out var actionID)) return true;

        if (IsOccupied()) return false;
        var state = CharacterManager.Instance()->LookupPetByOwnerObject(player.BattleChara()) != null;

        if (state) return true;

        return ActionManager.Instance()->UseAction(ActionType.Action, actionID);
    }

    public override void Uninit()
    {
        Service.DutyState.DutyRecommenced -= OnDutyRecommenced;
        Service.ClientState.TerritoryChanged -= OnZoneChanged;

        base.Uninit();
    }
}
