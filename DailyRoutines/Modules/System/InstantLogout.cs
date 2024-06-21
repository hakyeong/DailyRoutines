using DailyRoutines.Helpers;
using DailyRoutines.Managers;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;

namespace DailyRoutines.Modules;

[ModuleDescription("InstantLogoutTitle", "InstantLogoutDescription", ModuleCategories.系统)]
public unsafe class InstantLogout : DailyModuleBase
{
    private delegate nint SendLogoutDelegate();
    [Signature("40 53 48 83 EC ?? 48 8B 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B D8 48 85 C0 74 ?? 48 8B 0D")]
    private static SendLogoutDelegate? SendLogout;

    private delegate nint ExecuteLogoutCommandDelegate(uint* a1, nint a2, nint a3);
    [Signature("48 89 5C 24 ?? 56 48 81 EC ?? ?? ?? ?? 49 8B F0", DetourName = nameof(ExecuteLogoutCommandDetour))]
    private static Hook<ExecuteLogoutCommandDelegate>? ExecuteLogoutCommandHook;

    public override void Init()
    {
        Service.Hook.InitializeFromAttributes(this);
        ExecuteLogoutCommandHook.Enable();
    }

    private static nint ExecuteLogoutCommandDetour(uint* a1, nint a2, nint a3)
    {
        if (*(a1 + 32) > 0)
            SendLogout();
        else
            ChatHelper.Instance.SendMessage("/xlkill");

        return nint.Zero;
    }
}
