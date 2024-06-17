using System.Windows.Forms;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.System.String;

namespace DailyRoutines.Modules;

[ModuleDescription("ClipboardMultiLineToOneLineTitle", "ClipboardMultiLineToOneLineDescription",
                   ModuleCategories.系统)]
public unsafe class ClipboardMultiLineToOneLine : DailyModuleBase
{
    private delegate Utf8String* GetClipboardDataDelegate(nint a1);
    [Signature("40 53 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 48 8B D9 BA",
               DetourName = nameof(GetClipboardDataDetour))]
    private static Hook<GetClipboardDataDelegate>? GetClipboardDataHook;

    internal static bool IsBlocked;

    public override void Init()
    {
        Service.Hook.InitializeFromAttributes(this);
        GetClipboardDataHook?.Enable();

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "Macro", OnAddon);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "Macro", OnAddon);
    }

    private static void OnAddon(AddonEvent type, AddonArgs args)
    {
        IsBlocked = type switch
        {
            AddonEvent.PostSetup => true,
            AddonEvent.PreFinalize => false,
            _ => IsBlocked,
        };
    }

    private static Utf8String* GetClipboardDataDetour(nint a1)
    {
        if (IsBlocked || Framework.Instance()->WindowInactive) return GetClipboardDataHook.Original(a1);

        var copyModule = Framework.Instance()->GetUIClipboard();
        if (copyModule == null) return GetClipboardDataHook.Original(a1);

        var originalText = Clipboard.GetText();
        if (string.IsNullOrWhiteSpace(originalText)) return GetClipboardDataHook.Original(a1);

        var modifiedText = originalText.Replace("\r\n", " ").Replace("\n", " ").Replace("\u000D", " ")
                                       .Replace("\u000D\u000A", " ");

        if (modifiedText == originalText) return GetClipboardDataHook.Original(a1);

        return Utf8String.FromString(modifiedText);
    }

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddon);

        base.Uninit();
    }
}
