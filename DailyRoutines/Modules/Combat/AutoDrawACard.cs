using DailyRoutines.Infos;
using DailyRoutines.Managers;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoDrawACardTitle", "AutoDrawACardDescription", ModuleCategories.Combat)]
public class AutoDrawACard : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithConfigUI => false;

    private static TaskManager? TaskManager;

    public void Init()
    {
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 30000, ShowDebug = false };

        Service.ClientState.TerritoryChanged += OnZoneChanged;
    }

    public void ConfigUI() { }

    public void OverlayUI() { }

    private void OnZoneChanged(object? sender, ushort e)
    {
        if (!Service.PresetData.ContentTerritories.Contains(e)) return;
        TaskManager.Abort();
        TaskManager.Enqueue(CheckCurrentJob);
    }

    private static unsafe bool? CheckCurrentJob()
    {
        var player = Service.ClientState.LocalPlayer;
        if (player == null || player.ClassJob.Id == 0) return false;

        if (player.ClassJob.Id != 33 || player.Level < 30) return true;
        if (IsOccupied()) return false;

        return ActionManager.Instance()->UseAction(ActionType.Spell, 3590);
    }

    public void Uninit()
    {
        TaskManager?.Abort();
        Service.ClientState.TerritoryChanged -= OnZoneChanged;
    }
}
