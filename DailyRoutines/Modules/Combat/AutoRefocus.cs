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
    public bool WithUI => false;


    private delegate void SetFocusTargetByObjectIDDelegate(TargetSystem* targetSystem, long objectID);

    [Signature("E8 ?? ?? ?? ?? BA 0C 00 00 00 48 8D 0D", DetourName = nameof(SetFocusTargetByObjectID))]
    private Hook<SetFocusTargetByObjectIDDelegate>? setFocusTargetByObjectIDHook;

    private static HashSet<uint>? ContentTerritories;
    private static (string Name, ulong ObjectID)? FocusTarget;

    public void Init()
    {
        SignatureHelper.Initialise(this);
        setFocusTargetByObjectIDHook?.Enable();

        ContentTerritories ??= [.. Service.ExcelData.Contents.Keys];
        if (IsBoundByDuty()) OnZoneChange(null, 0);
        Service.ClientState.TerritoryChanged += OnZoneChange;
    }

    public void UI() { }

    private void OnZoneChange(object? sender, ushort e)
    {
        if (ContentTerritories.Contains(Service.ClientState.TerritoryType))
        {
            FocusTarget = null;
            Service.Framework.Update += OnUpdate;
        }
        else
        {
            FocusTarget = null;
            Service.Framework.Update -= OnUpdate;
            Service.Framework.Update -= OnUpdate;
            Service.Framework.Update -= OnUpdate;
        }
    }

    private void OnUpdate(Framework framework)
    {
        if (FocusTarget != null && Service.Target.FocusTarget == null)
            setFocusTargetByObjectIDHook.Original(TargetSystem.StaticAddressPointers.pInstance,
                                                  (long)FocusTarget.Value.ObjectID);
    }

    private void SetFocusTargetByObjectID(TargetSystem* targetSystem, long objectID)
    {
        if (objectID == 0xE000_0000)
        {
            objectID = Service.Target.Target?.ObjectId ?? 0xE000_0000;
            if (Service.Target.Target == null)
            {
                FocusTarget = null;
                Service.Log.Debug("已清除焦点目标");
            }
        }
        else
        {
            var targetInfo = (Service.Target.Target.Name.ExtractText(), Service.Target.Target.ObjectId);
            FocusTarget = targetInfo;
            Service.Log.Debug($"已设置焦点目标为 {targetInfo}");
        }

        setFocusTargetByObjectIDHook.Original(targetSystem, objectID);
    }

    public static bool IsBoundByDuty()
    {
        return Service.Condition[ConditionFlag.BoundByDuty] ||
               Service.Condition[ConditionFlag.BoundByDuty56] ||
               Service.Condition[ConditionFlag.BoundByDuty95];
    }

    public void Uninit()
    {
        setFocusTargetByObjectIDHook.Dispose();
        Service.ClientState.TerritoryChanged -= OnZoneChange;
        Service.Framework.Update -= OnUpdate;
        Service.Framework.Update -= OnUpdate;
        Service.Framework.Update -= OnUpdate;
    }
}
