using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Hooking;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("BiggerHDWindowTextTitle", "BiggerHDWindowTextDescription", ModuleCategories.界面)]
public unsafe class BiggerHDWindowText : DailyModuleBase
{
    private class TextNodeInfo(uint nodeID)
    {
        public uint NodeID { get; set; } = nodeID;
        public byte? TextFlag1 { get; set; }
        public byte? TextFlag2 { get; set; }
    }

    private delegate nint TextInputReceiveEventDelegate
        (AtkComponentTextInput* component, ushort eventCase, uint a3, nint a4, ushort* a5);
    [Signature("40 55 53 56 57 41 56 48 8D AC 24 ?? ?? ?? ?? 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 85 ?? ?? ?? ?? 48 8B 9D", DetourName = nameof(TextInputReceiveEventDetour))]
    private static Hook<TextInputReceiveEventDelegate>? TextInputReceiveEventHook;

    private static readonly Dictionary<string, TextNodeInfo> TextWindows = new()
    {
        { "LookingForGroupDetail", new TextNodeInfo(20) }
    };

    private static float FontScale = 2f;

    public override void Init()
    {
        AddConfig("FontScale", FontScale);
        FontScale = GetConfig<float>("FontScale");

        Service.Hook.InitializeFromAttributes(this);
        TextInputReceiveEventHook?.Enable();

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, TextWindows.Keys, OnTextAddon);
    }

    public override void ConfigUI()
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(ImGuiColors.DalamudOrange, $"{Service.Lang.GetText("BiggerHDWindowText-FontScale")}:");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(100f * ImGuiHelpers.GlobalScale);
        ImGui.InputFloat("###FontScaleInput", ref FontScale, 0, 0, "%.1f");
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            FontScale = (float)Math.Clamp(FontScale, 0.1, 5f);
            UpdateConfig(nameof(FontScale), FontScale);
        }
    }

    private static void OnTextAddon(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)args.Addon;
        if (addon == null) return;

        if (!TextWindows.TryGetValue(args.AddonName, out var info)) return;

        ModifyTextNode(addon, info.NodeID, true);
    }

    private static nint TextInputReceiveEventDetour
        (AtkComponentTextInput* component, ushort eventCase, uint a3, nint a4, ushort* a5)
    {
        var original = TextInputReceiveEventHook.Original(component, eventCase, a3, a4, a5);
        
        if (eventCase == 9)
            ModifyTextInputComponent(component);

        return original;
    }

    private static void ModifyTextInputComponent(AtkComponentTextInput* component)
    {
        if (component == null) return;

        var imeBackground = component->AtkComponentInputBase.AtkComponentBase.UldManager.SearchNodeById(4);
        if (imeBackground == null) return;

        imeBackground->SetScale(FontScale, FontScale);
    }

    private static void ModifyTextNode(AtkUnitBase* addon, uint nodeID, bool isAdd)
    {
        if (addon == null) return;

        var textNode = addon->GetNodeById(nodeID)->GetAsAtkTextNode();
        if (textNode == null) return;

        if (!TextWindows.TryGetValue(Marshal.PtrToStringUTF8((nint)addon->Name), out var info)) return;

        if (isAdd)
        {
            info.TextFlag1 ??= textNode->TextFlags;
            info.TextFlag2 ??= textNode->TextFlags;

            textNode->TextFlags = 195;
            textNode->TextFlags2 = 0;
        }
        else
        {
            if (info.TextFlag1 != null) textNode->TextFlags = (byte)info.TextFlag1;
            if (info.TextFlag2 != null) textNode->TextFlags2 = (byte)info.TextFlag2;
        }
    }

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnTextAddon);

        foreach (var window in TextWindows)
        {
            var addon = (AtkUnitBase*)Service.Gui.GetAddonByName(window.Key);
            if (addon == null) continue;

            ModifyTextNode(addon, window.Value.NodeID, false);
        }

        base.Uninit();
    }
}
