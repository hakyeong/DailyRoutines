using System.Collections.Generic;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Control;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoRefocusTitle", "AutoRefocusDescription", ModuleCategories.Combat)]
public unsafe class AutoRefocus : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithConfigUI => false;

    private delegate void SetFocusTargetByObjectIDDelegate(TargetSystem* targetSystem, long objectID);
    [Signature("E8 ?? ?? ?? ?? BA 0C 00 00 00 48 8D 0D", DetourName = nameof(SetFocusTargetByObjectID))]
    private Hook<SetFocusTargetByObjectIDDelegate>? setFocusTargetByObjectIDHook;

    private static HashSet<uint>? ContentTerritories;
    private static ulong? FocusTarget;

    public void Init()
    {
        SignatureHelper.Initialise(this);
        setFocusTargetByObjectIDHook?.Enable();

        ContentTerritories ??= [.. Service.ExcelData.Contents.Keys];
        if (IsBoundByDuty()) OnZoneChange(null, 0);
        Service.ClientState.TerritoryChanged += OnZoneChange;
    }

    public void ConfigUI() { }

    public void OverlayUI() { }

    private void OnZoneChange(object? sender, ushort e)
    {
        FocusTarget = null;
        if (ContentTerritories.Contains(Service.ClientState.TerritoryType))
            Service.Framework.Update += OnUpdate;
        else
            Service.Framework.Update -= OnUpdate;
    }

    private void OnUpdate(Framework framework)
    {
        if (FocusTarget != null && Service.Target.FocusTarget == null)
            setFocusTargetByObjectIDHook.Original(TargetSystem.StaticAddressPointers.pInstance, (long)FocusTarget);
    }

    private void SetFocusTargetByObjectID(TargetSystem* targetSystem, long objectID)
    {
        if (objectID == 0xE000_0000)
        {
            objectID = Service.Target.Target?.ObjectId ?? 0xE000_0000;
            FocusTarget = Service.Target.Target?.ObjectId;
        }
        else
        {
            FocusTarget = Service.Target.Target.ObjectId;
        }
        setFocusTargetByObjectIDHook.Original(targetSystem, objectID);
    }

    public static bool IsBoundByDuty() => Service.Condition[ConditionFlag.BoundByDuty] || Service.Condition[ConditionFlag.BoundByDuty56] || Service.Condition[ConditionFlag.BoundByDuty95];

    public void Uninit()
    {
        setFocusTargetByObjectIDHook.Dispose();
        Service.ClientState.TerritoryChanged -= OnZoneChange;
        Service.Framework.Update -= OnUpdate;
    }
}
