using System.Windows.Forms;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace DailyRoutines.Modules;

[ModuleDescription("ClipboardMultiLineToOneLineTitle", "ClipboardMultiLineToOneLineDescription", ModuleCategories.InterfaceExpand)]
public unsafe class ClipboardMultiLineToOneLine : DailyModuleBase
{
    private delegate nint AddonReceiveEventDelegate(
        AtkEventListener* self, AtkEventType eventType, uint eventParam, AtkEvent* eventData, ulong* inputData);

    private Hook<AddonReceiveEventDelegate>? ChatLogTextInputHook;

    private static bool IsOnChatLog;

    public override void Init()
    {
        if (Service.Gui.GetAddonByName("ChatLog") != nint.Zero)
            OnChatLogAddonSetup(AddonEvent.PostSetup, null);
        else
            Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "ChatLog", OnChatLogAddonSetup);

        Service.Framework.Update += OnUpdate;
    }

    private void OnChatLogAddonSetup(AddonEvent type, AddonArgs? args)
    {
        var addon = (AtkUnitBase*)Service.Gui.GetAddonByName("ChatLog");
        var address = (nint)addon->GetNodeById(5)->GetComponent()->AtkEventListener.vfunc[2];
        ChatLogTextInputHook ??=
            Service.Hook.HookFromAddress<AddonReceiveEventDelegate>(address, ChatLogTextInputDetour);
        ChatLogTextInputHook?.Enable();
    }

    private nint ChatLogTextInputDetour(
        AtkEventListener* self, AtkEventType eventType, uint eventParam, AtkEvent* eventData, ulong* inputData)
    {
        IsOnChatLog = eventType switch
        {
            AtkEventType.FocusStart => true,
            AtkEventType.FocusStop => false,
            _ => IsOnChatLog
        };

        return ChatLogTextInputHook.Original(self, eventType, eventParam, eventData, inputData);
    }

    private static void OnUpdate(IFramework framework)
    {
        if (!IsOnChatLog) return;
        if (!EzThrottler.Throttle("ClipboardMultiLineToOneLine", 100)) return;

        if (Framework.Instance()->WindowInactive) return;

        var copyModule = Framework.Instance()->GetUIClipboard();
        if (copyModule == null) return;

        var originalText = Clipboard.GetText();
        if (string.IsNullOrEmpty(originalText)) return;

        var modifiedText = originalText.Replace("\r\n", " ").Replace("\n", " ");
        if (modifiedText == originalText) return;

        Clipboard.SetText(modifiedText);
    }

    public override void Uninit()
    {
        Service.Framework.Update -= OnUpdate;
        base.Uninit();
    }
}
