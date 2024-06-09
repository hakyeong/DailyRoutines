using DailyRoutines.Helpers;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using Lumina.Excel.GeneratedSheets;
using GameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace DailyRoutines.Modules;

[ModuleDescription("OptimizedInteractionTitle", "OptimizedInteractionDescription", ModuleCategories.系统)]
public unsafe class OptimizedInteraction : DailyModuleBase
{
    // 当前位置无法进行该操作
    private delegate bool CameraObjectBlockedDelegate(nint a1, nint a2, nint a3);
    [Signature("E8 ?? ?? ?? ?? 84 C0 75 ?? B9 ?? ?? ?? ?? E8 ?? ?? ?? ?? EB ?? 40 B7",
               DetourName = nameof(CameraObjectBlockedDetour))]
    private static Hook<CameraObjectBlockedDelegate>? CameraObjectBlockedHook;

    // 目标处于视野之外
    private delegate bool IsObjectInViewRangeDelegate(TargetSystem* system, GameObject* gameObject);
    [Signature(
        "E8 ?? ?? ?? ?? 84 C0 75 ?? 48 8B 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B C8 48 8B 10 FF 52 ?? 48 8B C8 BA ?? ?? ?? ?? E8 ?? ?? ?? ?? E9",
        DetourName = nameof(IsObjectInViewRangeHookDetour))]
    private static Hook<IsObjectInViewRangeDelegate>? IsObjectInViewRangeHook;

    // 跳跃中无法进行该操作 / 飞行中无法进行该操作
    private delegate bool InteractCheck0Delegate(nint a1, GameObject* localPlayer, nint a3, nint a4, bool a5);
    [Signature("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC ?? 49 8B 00 49 8B C8", DetourName = nameof(InteractCheck0Detour))]
    private static Hook<InteractCheck0Delegate>? InteractCheck0Hook;

    // 跳跃中无法进行该操作
    private delegate bool IsPlayerOnJumpingDelegate(nint a1);
    [Signature("E8 ?? ?? ?? ?? 84 C0 0F 85 ?? ?? ?? ?? 48 8D 8D ?? ?? ?? ?? E8 ?? ?? ?? ?? 84 C0 0F 85",
               DetourName = nameof(IsPlayerOnJumpingDetour0))]
    private static Hook<IsPlayerOnJumpingDelegate>? IsPlayerOnJumping0Hook;

    [Signature("E8 ?? ?? ?? ?? 84 C0 0F 85 ?? ?? ?? ?? 48 8D 8D ?? ?? ?? ?? 48 89 9C 24",
               DetourName = nameof(IsPlayerOnJumpingDetour1))]
    private static Hook<IsPlayerOnJumpingDelegate>? IsPlayerOnJumping1Hook;

    // 检查目标距离 / 高低
    private delegate bool CheckTargetPositionDelegate(nint a1, nint a2, nint a3, byte a4, byte a5);
    [Signature("40 53 57 41 56 48 83 EC ?? 48 8B 02", DetourName = nameof(CheckTargetPositionDetour))]
    private static Hook<CheckTargetPositionDelegate>? CheckTargetPositionHook;

    public override void Init()
    {
        Service.Hook.InitializeFromAttributes(this);
        CameraObjectBlockedHook.Enable();
        IsObjectInViewRangeHook.Enable();
        InteractCheck0Hook.Enable();
        IsPlayerOnJumping0Hook.Enable();
        IsPlayerOnJumping1Hook.Enable();
        CheckTargetPositionHook.Enable();

        Service.ClientState.TerritoryChanged += OnZoneChanged;
        OnZoneChanged(Service.ClientState.TerritoryType);
    }

    private static void OnZoneChanged(ushort zone)
    {
        if (LuminaCache.GetRow<TerritoryType>(zone).IsPvpZone)
        {
            CameraObjectBlockedHook.Disable();
            IsObjectInViewRangeHook.Disable();
            InteractCheck0Hook.Disable();
            IsPlayerOnJumping0Hook.Disable();
            IsPlayerOnJumping1Hook.Disable();
            CheckTargetPositionHook.Disable();
        }
        else
        {
            CameraObjectBlockedHook.Enable();
            IsObjectInViewRangeHook.Enable();
            InteractCheck0Hook.Enable();
            IsPlayerOnJumping0Hook.Enable();
            IsPlayerOnJumping1Hook.Enable();
            CheckTargetPositionHook.Enable();
        }
    }

    private static bool CameraObjectBlockedDetour(nint a1, nint a2, nint a3)
    {
        return true;
    }

    private static bool IsObjectInViewRangeHookDetour(TargetSystem* system, GameObject* gameObject)
    {
        return true;
    }

    private static bool InteractCheck0Detour(nint a1, GameObject* localPlayer, nint a3, nint a4, bool a5)
    {
        return true;
    }

    private static bool IsPlayerOnJumpingDetour0(nint a1)
    {
        return false;
    }

    private static bool IsPlayerOnJumpingDetour1(nint a1)
    {
        return false;
    }

    private static bool CheckTargetPositionDetour(nint a1, nint a2, nint a3, byte a4, byte a5)
    {
        if (Flags.IsOnMount)
            Service.ExecuteCommandManager.ExecuteCommand(ExecuteCommandFlag.Dismount, 1);

        return true;
    }

    public override void Uninit()
    {
        Service.ClientState.TerritoryChanged -= OnZoneChanged;

        base.Uninit();
    }
}
