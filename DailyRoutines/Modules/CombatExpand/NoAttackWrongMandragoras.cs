using System.Collections.Generic;
using System.Linq;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;

namespace DailyRoutines.Modules;

[ModuleDescription("NoAttackWrongMandragorasTitle", "NoAttackWrongMandragorasDescription", ModuleCategories.CombatExpand)]
public unsafe class NoAttackWrongMandragoras : DailyModuleBase
{
    [Signature("48 83 EC 38 33 D2 C7 44 24 20 00 00 00 00 45 33 C9")]
    private static delegate* unmanaged<void> CancelCast;

    [Signature("E8 ?? ?? ?? ?? 44 0F B6 C3 48 8B D0")]
    private static delegate* unmanaged<ulong, FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*> GetGameObjectFromObjectID;

    private delegate bool UseActionSelfDelegate(
        ActionManager* actionManager, uint actionType, uint actionID, ulong targetID = 0xE000_0000, uint a4 = 0,
        uint a5 = 0, uint a6 = 0, void* a7 = null);
    private Hook<UseActionSelfDelegate>? useActionSelfHook;

    private static List<uint[]>? Mandragoras;
    private static readonly List<BattleNpc> ValidBattleNPCs = [];
    // 水城, 运河, 运河深层, 运河神殿, 梦羽宝境, 梦羽宝殿, 惊奇, 育体
    private static readonly HashSet<uint> ValidZones = [558, 712, 725, 794, 879, 924, 1000, 1123];

    private static bool OnlyInTreasureHunt;

    public override void Init()
    {
        Service.Hook.InitializeFromAttributes(this);

        useActionSelfHook =
            Service.Hook.HookFromAddress<UseActionSelfDelegate>((nint)ActionManager.MemberFunctionPointers.UseAction,
                                                                UseActionSelf);
        useActionSelfHook?.Enable();

        Mandragoras ??= Service.Data.GetExcelSheet<BNpcName>()
                               .Where(x => x.Singular.RawString.Contains("王后"))
                               .Select(queen => new[] { queen.RowId, queen.RowId - 1, queen.RowId - 2, queen.RowId - 3, queen.RowId - 4 })
                               .ToList();

        Service.Framework.Update += OnUpdate;

        AddConfig(this, "OnlyInTreasureHunt", true);
        OnlyInTreasureHunt = GetConfig<bool>(this, "OnlyInTreasureHunt");
    }

    public override void ConfigUI()
    {
        if (ImGui.Checkbox(Service.Lang.GetText("NoAttackWrongMandragoras-OnlyInTreasureHunt"), ref OnlyInTreasureHunt))
            UpdateConfig(this, "OnlyInTreasureHunt", OnlyInTreasureHunt);
    }

    private bool UseActionSelf(ActionManager* actionManager, uint actionType, uint actionID, ulong targetID, uint a4, uint a5, uint a6, void* a7)
    {
        if (OnlyInTreasureHunt && !ValidZones.Contains(Service.ClientState.TerritoryType))
            return useActionSelfHook.Original(actionManager, actionType, actionID, targetID, a4, a5, a6, a7);

        if (actionType != 1 || targetID == 0xE000_0000)
            return useActionSelfHook.Original(actionManager, actionType, actionID, targetID, a4, a5, a6, a7);

        var target = GetGameObjectFromObjectID(targetID);
        if (target == null)
            return useActionSelfHook.Original(actionManager, actionType, actionID, targetID, a4, a5, a6, a7);

        var objID = target->GetNpcID();
        ValidBattleNPCs.Clear();
        foreach (var obj in Service.ObjectTable)
        {
            if (obj.IsValid() && obj is BattleNpc { IsDead: false } battleObj)
            {
                ValidBattleNPCs.Add(battleObj);
            }
        }

        foreach (var mandragora in Mandragoras)
        {
            if (mandragora.Contains(objID))
            {
                if (mandragora.SkipWhile(id => id != objID).Skip(1).Take(5 - 1).Any(id => ValidBattleNPCs.Any(battleObj => battleObj.NameId == id)))
                {
                    Service.Target.Target = null;
                    CancelCast();
                    return false;
                }
            }
        }

        return useActionSelfHook.Original(actionManager, actionType, actionID, targetID, a4, a5, a6, a7);
    }

    private static void OnUpdate(IFramework framework)
    {
        if (OnlyInTreasureHunt && !ValidZones.Contains(Service.ClientState.TerritoryType)) return;

        var target = Service.Target.Target as BattleNpc;
        if (target == null) return;

        var objID = target.NameId;
    
        ValidBattleNPCs.Clear();
        foreach (var obj in Service.ObjectTable)
        {
            if (obj.IsValid() && obj is BattleNpc { IsDead: false } battleObj)
            {
                ValidBattleNPCs.Add(battleObj);
            }
        }

        foreach (var mandragora in Mandragoras)
        {
            if (mandragora.Contains(objID))
            {
                if (mandragora.SkipWhile(id => id != objID).Skip(1).Take(5 - 1).Any(id => ValidBattleNPCs.Any(battleObj => battleObj.NameId == id)))
                {
                    Service.Target.Target = null;
                    CancelCast();
                    return;
                }
            }
        }
    }

    public override void Uninit()
    {
        Service.Framework.Update -= OnUpdate;

        base.Uninit();
    }
}
