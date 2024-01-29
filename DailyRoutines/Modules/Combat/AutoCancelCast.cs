using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoCancelCastTitle", "AutoCancelCastDescription", ModuleCategories.Combat)]
public unsafe class AutoCancelCast : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithUI => false;

    [StructLayout(LayoutKind.Explicit)]
    internal struct ActionManagerEX
    {
        [FieldOffset(0x28)]
        public uint CastActionType;

        [FieldOffset(0x2C)]
        public uint CastActionID;

        [FieldOffset(0x38)]
        public uint CastTargetObjectID;
    }

    internal static ActionManagerEX ActionManagerData => *(ActionManagerEX*)ActionManager.Addresses.Instance.Value;

    [Signature("48 83 EC 38 33 D2 C7 44 24 20 00 00 00 00 45 33 C9")]
    private readonly delegate* unmanaged<void> CancelCast;

    [Signature("E8 ?? ?? ?? ?? 44 0F B6 C3 48 8B D0")]
    private readonly delegate* unmanaged<ulong, GameObject*> GetGameObjectFromObjectID;

    private static HashSet<uint>? TargetAreaActions;

    public void Init()
    {
        SignatureHelper.Initialise(this);
        Service.Condition.ConditionChange += OnConditionChanged;

        TargetAreaActions ??= Service.ExcelData.Actions.Where(x => x.Value.TargetArea).Select(x => x.Key).ToHashSet();
    }

    public void UI() { }

    private void OnConditionChanged(ConditionFlag flag, bool value)
    {
        if (flag is ConditionFlag.Casting or ConditionFlag.Casting87)
        {
            if (value)
                Service.Framework.Update += OnUpdate;
            else
            {
                Service.Framework.Update -= OnUpdate;
                Service.Framework.Update -= OnUpdate;
            }
        }
    }

    private void OnUpdate(Framework framework)
    {
        if (ActionManagerData.CastActionType != 1 || TargetAreaActions.Contains(ActionManagerData.CastActionID)) return;
        var obj = GetGameObjectFromObjectID(ActionManagerData.CastTargetObjectID);
        if (obj == null || ActionManager.CanUseActionOnTarget(ActionManagerData.CastActionID, obj)) return;

        CancelCast();
    }

    public void Uninit()
    {
        Service.Condition.ConditionChange -= OnConditionChanged;
        Service.Framework.Update -= OnUpdate;
        Service.Framework.Update -= OnUpdate;
    }
}
