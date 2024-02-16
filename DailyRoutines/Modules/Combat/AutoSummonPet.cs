using DailyRoutines.Infos;
using DailyRoutines.Managers;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.Game;
using System.Collections.Generic;
using ECommons.GameFunctions;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using BattleChara = FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoSummonPetTitle", "AutoSummonPetDescription", ModuleCategories.Combat)]
public class AutoSummonPet : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithConfigUI => false;

    private static TaskManager? TaskManager;

    private static readonly Dictionary<uint, uint> SummonActions = new()
    {
        // 学者
        { 28, 17215 },
        // 秘术师 / 召唤师
        { 26, 25798 },
        { 27, 25798 },
    };

    public void Init()
    {
        TaskManager = new TaskManager { AbortOnTimeout = true, TimeLimitMS = 30000, ShowDebug = false };

        Service.ClientState.TerritoryChanged += OnZoneChanged;
    }

    public void ConfigUI() { }

    public void OverlayUI() { }

    private void OnZoneChanged(object? sender, ushort e)
    {
        if (!Service.ExcelData.ContentTerritories.Contains(e)) return;
        TaskManager.Abort();
        TaskManager.Enqueue(CheckCurrentJob);
    }

    private static unsafe bool? CheckCurrentJob()
    {
        var player = Service.ClientState.LocalPlayer;
        if (player == null || player.ClassJob.Id == 0) return false;

        var job = player.ClassJob.Id;
        if (!SummonActions.TryGetValue(job, out var actionID)) return true;

        if (IsOccupied()) return false;
        var state = CharacterManager.Instance()->LookupPetByOwnerObject(player.BattleChara()) != null;

        if (state) return true;

        return ActionManager.Instance()->UseAction(ActionType.Spell, actionID);
    }

    public void Uninit()
    {
        TaskManager?.Abort();
        Service.ClientState.TerritoryChanged -= OnZoneChanged;
    }
}
