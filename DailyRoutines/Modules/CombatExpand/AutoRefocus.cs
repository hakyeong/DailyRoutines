using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game.Control;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoRefocusTitle", "AutoRefocusDescription", ModuleCategories.CombatExpand)]
public unsafe class AutoRefocus : DailyModuleBase
{
    private delegate void SetFocusTargetByObjectIDDelegate(TargetSystem* targetSystem, long objectID);

    [Signature("E8 ?? ?? ?? ?? BA 0C 00 00 00 48 8D 0D", DetourName = nameof(SetFocusTargetByObjectID))]
    private static Hook<SetFocusTargetByObjectIDDelegate>? setFocusTargetByObjectIDHook;

    private static ulong? FocusTarget;
    private static bool IsNeedToRefocus;

    public override void Init()
    {
        Service.Hook.InitializeFromAttributes(this);
        setFocusTargetByObjectIDHook?.Enable();

        if (IsBoundByDuty()) OnZoneChange(Service.ClientState.TerritoryType);
        Service.ClientState.TerritoryChanged += OnZoneChange;
        Service.FrameworkManager.Register(OnUpdate);
    }

    private static void OnZoneChange(ushort territory)
    {
        FocusTarget = null;
        IsNeedToRefocus = Service.PresetData.Contents.ContainsKey(territory);
    }

    private static void OnUpdate(IFramework framework)
    {
        if (!IsNeedToRefocus) return;
        if (EzThrottler.Throttle("AutoRefocus"))
        {
            if (FocusTarget != null && Service.Target.FocusTarget == null)
                setFocusTargetByObjectIDHook.Original(TargetSystem.StaticAddressPointers.pInstance, (long)FocusTarget);
        }
    }

    private static void SetFocusTargetByObjectID(TargetSystem* targetSystem, long objectID)
    {
        if (objectID == 0xE000_0000)
        {
            objectID = Service.Target.Target?.ObjectId ?? 0xE000_0000;
            FocusTarget = Service.Target.Target?.ObjectId;
        }
        else
            FocusTarget = Service.Target.Target.ObjectId;

        setFocusTargetByObjectIDHook.Original(targetSystem, objectID);
    }

    public static bool IsBoundByDuty()
    {
        return Service.Condition[ConditionFlag.BoundByDuty] || Service.Condition[ConditionFlag.BoundByDuty56] ||
               Service.Condition[ConditionFlag.BoundByDuty95];
    }

    public override void Uninit()
    {
        Service.ClientState.TerritoryChanged -= OnZoneChange;

        base.Uninit();
    }
}
