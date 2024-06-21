using DailyRoutines.Managers;
using Dalamud.Hooking;
using Dalamud.Interface.Internal;
using Dalamud.Interface.Utility;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;

namespace DailyRoutines.Modules.UIOptimization;

[ModuleDescription("MarkInPartyListTitle", "PartyFinderFilterDescription", ModuleCategories.界面优化)]
public class MarkInPartyList : DailyModuleBase
{
    public override string? Author => "status102";

    private const ImGuiWindowFlags Flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoMouseInputs | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoNav;
    private Vector2 PartyListIconOffset = new(0, 0);
    private float PartyListIconScale = 1f;
    private Dictionary<MarkIcon, IDalamudTextureWrap> _markIcon = [];
    private readonly Dictionary<MarkIcon, int> _markedObject = new(8);
    private bool _needClear;

    public override void Init()
    {
        Service.Hook.InitializeFromAttributes(this);
        try
        {
            _markIcon = Enum.GetValues<MarkIcon>().ToDictionary(x => x, x => Service.Texture.GetIcon((uint)x)!);
        }
        catch (Exception e)
        {
            Service.Log.Error(e, "Failed to load textures");
        }
        LocalMarkingHook?.Enable();
        Service.ClientState.TerritoryChanged += ResetmarkedObject;
    }

    public override unsafe void Uninit()
    {
        LocalMarkingHook?.Dispose();
        Service.ClientState.TerritoryChanged -= ResetmarkedObject;
        foreach (var i in _markIcon.Values)
        {
            i?.Dispose();
        }
        ResetPartyMemberList();

        base.Uninit();
    }

    public override void ConfigUI()
    {
    }

    public override unsafe void OverlayUI()
    {
        if (_markedObject.Count != 0)
        {
        }
        else if (_needClear)
        {
            ResetPartyMemberList();
            _needClear = false;
            return;
        }
        else
        {
            return;
        }

        var partylist = (AtkUnitBase*)Service.Gui.GetAddonByName("_PartyList");
        if (partylist is null || !partylist->IsVisible || partylist->UldManager.LoadedState != AtkLoadState.Loaded || Service.ClientState.LocalPlayer is null)
        {
            return;
        }

        ModifyPartyMemberNumber(partylist, false);

        ImGuiHelpers.ForceNextWindowMainViewport();
        ImGui.SetNextWindowPos(ImGui.GetMainViewport().Pos);
        ImGui.SetNextWindowSize(ImGui.GetMainViewport().Size);
        if (ImGui.Begin("##MarkOverlayWindow", Flags))
        {
            foreach (var icon in _markedObject)
            {
                DrawOnPartyList(icon.Value, icon.Key, partylist, ImGui.GetWindowDrawList());
            }
            ImGui.End();
        }

    }
    private unsafe void ResetmarkedObject(ushort obj)
    {
        _markedObject.Clear();
        ResetPartyMemberList();
    }

    private unsafe void ResetPartyMemberList(AtkUnitBase* partylist = null)
    {
        if (partylist is null)
        {
            partylist = (AtkUnitBase*)Service.Gui.GetAddonByName("_PartyList");
        }

        if (partylist is not null && partylist->IsVisible)
        {
            ModifyPartyMemberNumber(partylist, true);
        }
    }

    private unsafe void DrawOnPartyList(int listIndex, MarkIcon markIcon, AtkUnitBase* pPartyList, ImDrawListPtr drawList)
    {
        if (listIndex < 0 || listIndex > 7)
        {
            return;
        }

        int partyMemberNodeIndex = 22 - listIndex;
        int iconNodeIndex = 4;
        var partyAlign = pPartyList->UldManager.NodeList[3]->Y;

        var pPartyMemberNode = pPartyList->UldManager.NodeListSize > partyMemberNodeIndex ? (AtkComponentNode*)pPartyList->UldManager.NodeList[partyMemberNodeIndex] : null;
        if (pPartyMemberNode is null)
        {
            return;
        }
        var pIconNode = pPartyMemberNode->Component->UldManager.NodeListSize > iconNodeIndex ? pPartyMemberNode->Component->UldManager.NodeList[iconNodeIndex] : null;
        if (pIconNode is null)
        {
            return;
        }
        //	Note: sub-nodes don't scale, so we have to account for the addon's scale.
        Vector2 iconOffset = (new Vector2(5, -5) + PartyListIconOffset) * pPartyList->Scale;
        Vector2 iconSize = new Vector2(pIconNode->Width / 2, pIconNode->Height / 2) * PartyListIconScale * 0.9f * pPartyList->Scale;
        Vector2 iconPos = new Vector2(pPartyList->X + pPartyMemberNode->AtkResNode.X * pPartyList->Scale + pIconNode->X * pPartyList->Scale + pIconNode->Width * pPartyList->Scale / 2,
                                        pPartyList->Y + partyAlign + pPartyMemberNode->AtkResNode.Y * pPartyList->Scale + pIconNode->Y * pPartyList->Scale + pIconNode->Height * pPartyList->Scale / 2);
        iconPos += iconOffset;
        drawList.AddImage(this._markIcon[markIcon].ImGuiHandle, iconPos, iconPos + iconSize);
    }

    private unsafe void ModifyPartyMemberNumber(AtkUnitBase* pPartyList, bool visible)
    {
        if (pPartyList == null)
        {
            return;
        }

        var memberIdList = Enumerable.Range(10, 17).ToList();
        foreach (var id in memberIdList)
        {
            var member = pPartyList->GetNodeById((uint)id);
            if (member is null || member->GetComponent() is null)
            {
                continue;
            }
            else if (!member->IsVisible)
            {
                break;
            }
            var textNode = member->GetComponent()->UldManager.SearchNodeById(15);
            if (textNode != null && textNode->IsVisible != visible)
            {
                textNode->ToggleVisibility(visible);
            }
        }
    }


    private unsafe void ProcMarkIconSetted(MarkType markType, uint objectId)
    {
        var icon = Enum.Parse<MarkIcon>(Enum.GetName(markType) ?? string.Empty);
        if (objectId == 0xE000_0000 || objectId == 0xE00_0000)
        {
            _markedObject.Remove(icon);
            if (_markedObject.Count == 0)
            {
                _needClear = true;
            }
            return;
        }
        else if (objectId < 0x1000_0000 || objectId > 0x2000_0000)
        {
            return;
        }

        if (Framework.Instance() is null)
        {
            return;
        }
        var pAgentHUD = Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentHUD();
        if (GroupManager.Instance()->MemberCount > 0)
        {
            for (var i = 0; i < 8; ++i)
            {
                var offset = i * Marshal.SizeOf<PartyListCharInfo>();
                var pCharData = pAgentHUD->PartyMemberList + offset;
                var charData = *(PartyListCharInfo*)pCharData;
                if (objectId > 0 && objectId == charData.ObjectID)
                {
                    if (_markedObject.ContainsValue(i))
                    {
                        _markedObject.Remove(_markedObject.First(x => x.Value == i).Key);
                    }
                    _needClear = false;
                    _markedObject[icon] = i;
                    return;
                }
            }
        }
    }

    [Signature("E8 ?? ?? ?? ?? 4C 8B C5 8B D7 48 8B CB E8", DetourName = nameof(DetourLocalMarkingFunc))]
    public static Hook<LocalMarkingFunc>? LocalMarkingHook;

    public delegate nint LocalMarkingFunc(nint manager, uint markingType, nint objectId, nint a4);
    private nint DetourLocalMarkingFunc(nint manager, uint markingType, nint objectId, nint a4)
    {
        ProcMarkIconSetted((MarkType)markingType, (uint)objectId);

        return LocalMarkingHook!.Original(manager, markingType, objectId, a4);
    }
}



[StructLayout(LayoutKind.Explicit, Size = 0x20)]
internal struct PartyListCharInfo
{
    [FieldOffset(0x00)] internal IntPtr ObjectAddress;
    [FieldOffset(0x08)] internal IntPtr ObjectNameAddress;
    [FieldOffset(0x10)] internal ulong ContentID;
    [FieldOffset(0x18)] internal uint ObjectID;
    [FieldOffset(0x1C)] internal uint Unknown;

    internal string GetName()
    {
        if (ObjectAddress == IntPtr.Zero || ObjectNameAddress == IntPtr.Zero)
        {
            return "";
        }

        return Marshal.PtrToStringUTF8(ObjectNameAddress) ?? "";
    }
}

public enum MarkType : byte
{
    Attack1 = 0,
    Attack2,
    Attack3,
    Attack4,
    Attack5,
    Bind1,
    Bind2,
    Bind3,
    Stop1,
    Stop2,
    Square,
    Circle,
    Cross,
    Triangle,
    Attack6,
    Attack7,
    Attack8,
}

public enum MarkIcon : uint
{
    Attack1 = 61201,
    Attack2,
    Attack3,
    Attack4,
    Attack5,
    Attack6,
    Attack7,
    Attack8,
    Bind1 = 61211,
    Bind2,
    Bind3,
    Stop1 = 61221,
    Stop2,
    Square = 61231,
    Circle,
    Cross,
    Triangle,
}
