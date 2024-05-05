using System.Collections.Generic;
using System.Linq;
using DailyRoutines.Helpers;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Utility.Signatures;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Lumina.Excel.GeneratedSheets;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoCancelCastTitle", "AutoCancelCastDescription", ModuleCategories.Action)]
public unsafe class AutoCancelCast : DailyModuleBase
{
    [Signature("48 83 EC 38 33 D2 C7 44 24 20 00 00 00 00 45 33 C9")]
    private readonly delegate* unmanaged<void> CancelCast;

    [Signature("E8 ?? ?? ?? ?? 44 0F B6 C3 48 8B D0")]
    private readonly delegate* unmanaged<ulong, GameObject*> GetGameObjectFromObjectID;

    private static HashSet<uint>? TargetAreaActions;

    public override void Init()
    {
        Service.Hook.InitializeFromAttributes(this);
        Service.Condition.ConditionChange += OnConditionChanged;
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 10000, ShowDebug = false };

        TargetAreaActions ??= LuminaCache.Get<Action>()
                                         .Where(x => x.TargetArea)
                                         .Select(x => x.RowId).ToHashSet();
    }

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
        var player = Service.ClientState.LocalPlayer;
        if (player.CastActionType != 1 || TargetAreaActions.Contains(player.CastActionId)) return true;
        var obj = GetGameObjectFromObjectID(player.CastTargetObjectId);
        if (obj == null || ActionManager.CanUseActionOnTarget(player.CastActionId, obj)) return false;

        CancelCast();
        return true;
    }

    public override void Uninit()
    {
        Service.Condition.ConditionChange -= OnConditionChanged;

        base.Uninit();
    }
}
