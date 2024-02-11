using System.Collections.Generic;
using System.Linq;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Utility.Signatures;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoCancelCastTitle", "AutoCancelCastDescription", ModuleCategories.Combat)]
public unsafe class AutoCancelCast : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithConfigUI => false;

    [Signature("48 83 EC 38 33 D2 C7 44 24 20 00 00 00 00 45 33 C9")]
    private readonly delegate* unmanaged<void> CancelCast;

    [Signature("E8 ?? ?? ?? ?? 44 0F B6 C3 48 8B D0")]
    private readonly delegate* unmanaged<ulong, GameObject*> GetGameObjectFromObjectID;

    private static TaskManager? TaskManager;

    private static HashSet<uint>? TargetAreaActions;

    public void Init()
    {
        SignatureHelper.Initialise(this);
        Service.Condition.ConditionChange += OnConditionChanged;
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 10000, ShowDebug = true };

        TargetAreaActions ??= Service.ExcelData.Actions.Where(x => x.Value.TargetArea).Select(x => x.Key).ToHashSet();
    }

    public void ConfigUI() { }

    public void OverlayUI() { }

    private void OnConditionChanged(ConditionFlag flag, bool value)
    {
        if (flag is ConditionFlag.Casting or ConditionFlag.Casting87)
        {
            if (value)
                TaskManager.Enqueue(IsNeedToCancel);
            else
                TaskManager.Abort();
        }
    }

    private bool? IsNeedToCancel()
    {
        if (Service.ClientState.LocalPlayer.CastActionType != 1 || TargetAreaActions.Contains(Service.ClientState.LocalPlayer.CastActionId)) return false;
        var obj = GetGameObjectFromObjectID(Service.ClientState.LocalPlayer.CastTargetObjectId);
        if (obj == null || ActionManager.CanUseActionOnTarget(Service.ClientState.LocalPlayer.CastActionId, obj)) return false;

        CancelCast();
        return true;
    }

    public void Uninit()
    {
        Service.Condition.ConditionChange -= OnConditionChanged;
        TaskManager?.Abort();
    }
}
