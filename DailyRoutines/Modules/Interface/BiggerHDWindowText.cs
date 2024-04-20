using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("BiggerHDWindowTextTitle", "BiggerHDWindowTextDescription", ModuleCategories.Interface)]
public unsafe class BiggerHDWindowText : DailyModuleBase
{
    private class TextNodeInfo(uint nodeID)
    {
        public uint NodeID { get; set; } = nodeID;
        public byte? TextFlag1 { get; set; }
        public byte? TextFlag2 { get; set; }
    }

    private static readonly string[] TextInputWindows = ["LookingForGroupCondition", "ChatLog", "AOZNotebookFilterSettings",
        "MountNoteBook", "MinionNoteBook", "ItemSearch", "PcSearchDetail", "Macro", "Emote", "LookingForGroupNameSearch", "InputString", "RecipeNote", "GatheringNote", "FishGuide2"];

    private static readonly Dictionary<string, uint[]> TextInputWindowsNodes = [];

    private static readonly Dictionary<string, TextNodeInfo> TextWindows = new()
    {
        { "LookingForGroupDetail", new TextNodeInfo(20) }
    };

    private static float FontScale = 2f;

    public override void Init()
    {
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, TextInputWindows, OnTextInputAddon);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, TextWindows.Keys, OnTextAddon);

        AddConfig("FontScale", FontScale);
        FontScale = GetConfig<float>("FontScale");
    }

    public override void ConfigUI()
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(ImGuiColors.DalamudOrange, $"{Service.Lang.GetText("BiggerHDWindowText-FontScale")}:");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(100f * ImGuiHelpers.GlobalScale);
        if (ImGui.InputFloat("###FontScaleInput", ref FontScale, 0, 0, "%.1f",
                             ImGuiInputTextFlags.EnterReturnsTrue))
        {
            FontScale = (float)Math.Clamp(FontScale, 0.1, 5f);
            UpdateConfig("FontScale", FontScale);
        }
    }

    private static void OnTextAddon(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)args.Addon;
        if (addon == null) return;

        if (!TextWindows.TryGetValue(args.AddonName, out var info)) return;

        ModifyTextNode(addon, info.NodeID, true);
    }

    private static void OnTextInputAddon(AddonEvent type, AddonArgs? args)
    {
        foreach (var window in TextInputWindows)
        {
            var addon = (AtkUnitBase*)Service.Gui.GetAddonByName(window);
            if (addon == null) continue;

            if (!TryScanTextInputComponent(addon, out var nodeID)) continue;

            foreach (var id in nodeID)
            {
                ModifyTextInputComponent(addon, id, true);
            }
        }
    }

    private static void ModifyTextInputComponent(AtkUnitBase* addon, uint nodeID, bool isAdd)
    {
        if (addon == null) return;

        var textInputNode = addon->GetNodeById(nodeID);
        if (textInputNode == null) return;

        var imeBackground = textInputNode->GetComponent()->UldManager.SearchNodeById(4);
        if (imeBackground == null) return;

        if (isAdd)
            imeBackground->SetScale(FontScale, FontScale);
        else
            imeBackground->SetScale(1.5f, 1.5f);
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

    private static bool TryScanTextInputComponent(AtkUnitBase* addon, out uint[] nodeID)
    {
        var addonName = Marshal.PtrToStringUTF8((nint)addon->Name);
        if (TextInputWindowsNodes.TryGetValue(addonName, out var nodes))
        {
            nodeID = nodes;
            return true;
        }

        nodeID = [];

        if (addon == null) return false;

        var nodeList = addon->UldManager.NodeList;

        var nodeIDList = new List<uint>();
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = nodeList[i];
            if (node == null) continue;
            if ((int)node->Type <= 1000) continue;

            var compNode = (AtkComponentNode*)node;
            var componentInfo = compNode->Component->UldManager;
            var objectInfo = (AtkUldComponentInfo*)componentInfo.Objects;
            if (objectInfo == null) continue;

            if (objectInfo->ComponentType == ComponentType.TextInput)
            {
                nodeIDList.Add(node->NodeID);
            }
        }
        nodeID = [.. nodeIDList];
        TextInputWindowsNodes[addonName] = nodeID;

        return nodeIDList.Count > 0;
    }

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnTextInputAddon);
        Service.AddonLifecycle.UnregisterListener(OnTextAddon);

        foreach (var window in TextWindows)
        {
            var addon = (AtkUnitBase*)Service.Gui.GetAddonByName(window.Key);
            if (addon == null) continue;

            ModifyTextNode(addon, window.Value.NodeID, false);
        }

        foreach (var window in TextInputWindows)
        {
            var addon = (AtkUnitBase*)Service.Gui.GetAddonByName(window);
            if (addon == null) continue;
            if (!TryScanTextInputComponent(addon, out var nodeID)) continue;

            foreach (var id in nodeID)
            {
                ModifyTextInputComponent(addon, id, false);
            }
        }

        base.Uninit();
    }
}
