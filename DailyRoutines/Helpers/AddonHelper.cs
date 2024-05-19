using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using DailyRoutines.Managers;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace DailyRoutines.Helpers;

public static unsafe class AddonHelper
{
    public record PartInfo(ushort U, ushort V, ushort Width, ushort Height);

    public delegate byte FireCallbackDelegate(AtkUnitBase* Base, int valueCount, AtkValue* values, byte updateState);
    public static FireCallbackDelegate? FireCallback;

    public delegate int GetAtkValueIntDelegate(nint address);
    public static GetAtkValueIntDelegate? GetAtkValueInt;

    public delegate byte* GetAtkValueStringDelegate(nint address);
    public static GetAtkValueStringDelegate? GetAtkValueString;

    public delegate AtkUnitBase* GetAddonByNodeDelegate(nint atkstageInstance, AtkComponentNode* ownerNode);
    public static GetAddonByNodeDelegate? GetAddonByNode;

    public delegate void SetComponentButtonCheckedDelegate(AtkComponentButton* button, bool isChecked);
    public static SetComponentButtonCheckedDelegate? SetComponentButtonChecked;

    public static readonly AtkValue ZeroAtkValue = new() { Type = 0, Int = 0 };

    internal static void Init()
    {
        FireCallback ??= Marshal.GetDelegateForFunctionPointer<FireCallbackDelegate>(Service.SigScanner.ScanText("E8 ?? ?? ?? ?? 8B 4C 24 20 0F B6 D8"));
        GetAtkValueInt ??= Marshal.GetDelegateForFunctionPointer<GetAtkValueIntDelegate>(Service.SigScanner.ScanText("E8 ?? ?? ?? ?? C6 45 ?? ?? 8D 48"));
        GetAtkValueString ??= Marshal.GetDelegateForFunctionPointer<GetAtkValueStringDelegate>(Service.SigScanner.ScanText("E8 ?? ?? ?? ?? 33 D2 48 8D 8B ?? ?? ?? ?? 41 B8 ?? ?? ?? ?? 48 8B F8"));
        GetAddonByNode ??= Marshal.GetDelegateForFunctionPointer<GetAddonByNodeDelegate>(Service.SigScanner.ScanText("48 83 EC ?? 4C 8B D2 4C 8B D9 48 85 D2 75"));
        SetComponentButtonChecked ??= Marshal.GetDelegateForFunctionPointer<SetComponentButtonCheckedDelegate>(Service.SigScanner.ScanText("E8 ?? ?? ?? ?? 0F B7 DD"));
    }

    #region Callback
    public static void CallbackRaw(AtkUnitBase* Base, int valueCount, AtkValue* values, byte updateState = 0)
    {
        if (FireCallback == null) Init();
        FireCallback(Base, valueCount, values, updateState);
    }

    public static void Callback(AtkUnitBase* Base, bool updateState, params object[] values)
    {
        if (Base == null) throw new Exception("Null UnitBase");
        var atkValues = CreateAtkValueArray(values);
        if (atkValues == null) return;

        try
        {
            CallbackRaw(Base, values.Length, atkValues, (byte)(updateState ? 1 : 0));
        }
        finally
        {
            for (var i = 0; i < values.Length; i++)
                if (atkValues[i].Type == ValueType.String)
                    Marshal.FreeHGlobal(new nint(atkValues[i].String));
            Marshal.FreeHGlobal(new nint(atkValues));
        }
    }

    public static AtkValue* CreateAtkValueArray(params object[] values)
    {
        var atkValues = (AtkValue*)Marshal.AllocHGlobal(values.Length * sizeof(AtkValue));
        if (atkValues == null) return null;
        try
        {
            for (var i = 0; i < values.Length; i++)
            {
                var v = values[i];
                switch (v)
                {
                    case uint uintValue:
                        atkValues[i].Type = ValueType.UInt;
                        atkValues[i].UInt = uintValue;
                        break;
                    case int intValue:
                        atkValues[i].Type = ValueType.Int;
                        atkValues[i].Int = intValue;
                        break;
                    case float floatValue:
                        atkValues[i].Type = ValueType.Float;
                        atkValues[i].Float = floatValue;
                        break;
                    case bool boolValue:
                        atkValues[i].Type = ValueType.Bool;
                        atkValues[i].Byte = (byte)(boolValue ? 1 : 0);
                        break;
                    case string stringValue:
                        {
                            atkValues[i].Type = ValueType.String;
                            var stringBytes = Encoding.UTF8.GetBytes(stringValue);
                            var stringAlloc = Marshal.AllocHGlobal(stringBytes.Length + 1);
                            Marshal.Copy(stringBytes, 0, stringAlloc, stringBytes.Length);
                            Marshal.WriteByte(stringAlloc, stringBytes.Length, 0);
                            atkValues[i].String = (byte*)stringAlloc;
                            break;
                        }
                    default:
                        throw new ArgumentException($"Unable to convert type {v.GetType()} to AtkValue");
                }
            }
        }
        catch
        {
            return null;
        }

        return atkValues;
    }

    public static string DecodeValues(int cnt, AtkValue* values)
    {
        var atkValueList = new List<string>();
        try
        {
            for (var i = 0; i < cnt; i++) atkValueList.Add(DecodeValue(values[i]));
        }
        catch (Exception e)
        {
            e.Log();
        }

        return atkValueList.Join("\n");
    }

    public static string DecodeValue(AtkValue a)
    {
        var str = new StringBuilder(a.Type.ToString()).Append(": ");
        switch (a.Type)
        {
            case ValueType.Int:
                {
                    str.Append(a.Int);
                    break;
                }
            case ValueType.String:
                {
                    str.Append(Marshal.PtrToStringUTF8(new nint(a.String)));
                    break;
                }
            case ValueType.UInt:
                {
                    str.Append(a.UInt);
                    break;
                }
            case ValueType.Bool:
                {
                    str.Append(a.Byte != 0);
                    break;
                }
            default:
                {
                    str.Append($"Unknown Type: {a.Int}");
                    break;
                }
        }

        return str.ToString();
    }
    #endregion

    #region AddonFireback
    public static bool? ClickContextMenuByText(string text)
    {
        if (TryGetAddonByName<AtkUnitBase>("ContextMenu", out var addon) && IsAddonAndNodesReady(addon))
        {
            if (!TryScanContextMenuText(addon, text, out var index))
            {
                addon->FireCloseCallback();
                addon->Close(true);
                return true;
            }

            Callback(addon, true, 0, index, 0U, 0, 0);
        }

        return false;
    }
    #endregion

    #region NodeManagement
    public static AtkImageNode* MakeImageNode(uint id, PartInfo partInfo)
    {
        if (!TryMakeImageNode(id, 0, 0, 0, 0, out var imageNode))
        {
            Service.Log.Error("Failed to alloc memory for AtkImageNode.");
            return null;
        }

        if (!TryMakePartsList(0, out var partsList))
        {
            Service.Log.Error("Failed to alloc memory for AtkUldPartsList.");
            FreeImageNode(imageNode);
            return null;
        }

        if (!TryMakePart(partInfo.U, partInfo.V, partInfo.Width, partInfo.Height, out var part))
        {
            Service.Log.Error("Failed to alloc memory for AtkUldPart.");
            FreePartsList(partsList);
            FreeImageNode(imageNode);
            return null;
        }

        if (!TryMakeAsset(0, out var asset))
        {
            Service.Log.Error("Failed to alloc memory for AtkUldAsset.");
            FreePart(part);
            FreePartsList(partsList);
            FreeImageNode(imageNode);
        }

        AddAsset(part, asset);
        AddPart(partsList, part);
        AddPartsList(imageNode, partsList);

        return imageNode;
    }

    public static bool TryMakeImageNode(
        uint id, NodeFlags resNodeFlags, uint resNodeDrawFlags, byte wrapMode, byte imageNodeFlags,
        [NotNullWhen(true)] out AtkImageNode* imageNode)
    {
        imageNode = IMemorySpace.GetUISpace()->Create<AtkImageNode>();

        if (imageNode is not null)
        {
            imageNode->AtkResNode.Type = NodeType.Image;
            imageNode->AtkResNode.NodeID = id;
            imageNode->AtkResNode.NodeFlags = resNodeFlags;
            imageNode->AtkResNode.DrawFlags = resNodeDrawFlags;
            imageNode->WrapMode = wrapMode;
            imageNode->Flags = imageNodeFlags;
            return true;
        }

        return false;
    }

    public static bool TryMakePartsList(uint id, [NotNullWhen(true)] out AtkUldPartsList* partsList)
    {
        partsList = (AtkUldPartsList*)IMemorySpace.GetUISpace()->Malloc((ulong)sizeof(AtkUldPartsList), 8);

        if (partsList is not null)
        {
            partsList->Id = id;
            partsList->PartCount = 0;
            partsList->Parts = null;
            return true;
        }

        return false;
    }

    public static bool TryMakePart(
        ushort u, ushort v, ushort width, ushort height, [NotNullWhen(true)] out AtkUldPart* part)
    {
        part = (AtkUldPart*)IMemorySpace.GetUISpace()->Malloc((ulong)sizeof(AtkUldPart), 8);

        if (part is not null)
        {
            part->U = u;
            part->V = v;
            part->Width = width;
            part->Height = height;
            return true;
        }

        return false;
    }

    public static bool TryMakeAsset(uint id, [NotNullWhen(true)] out AtkUldAsset* asset)
    {
        asset = (AtkUldAsset*)IMemorySpace.GetUISpace()->Malloc((ulong)sizeof(AtkUldAsset), 8);

        if (asset is not null)
        {
            asset->Id = id;
            asset->AtkTexture.Ctor();
            return true;
        }

        return false;
    }

    public static void AddPartsList(AtkImageNode* imageNode, AtkUldPartsList* partsList)
    {
        imageNode->PartsList = partsList;
    }

    public static void AddPartsList(AtkCounterNode* counterNode, AtkUldPartsList* partsList)
    {
        counterNode->PartsList = partsList;
    }

    public static void AddPart(AtkUldPartsList* partsList, AtkUldPart* part)
    {
        // copy pointer to old array
        var oldPartArray = partsList->Parts;

        // allocate space for new array
        var newSize = partsList->PartCount + 1;
        var newArray = (AtkUldPart*)IMemorySpace.GetUISpace()->Malloc((ulong)sizeof(AtkUldPart) * newSize, 8);

        if (oldPartArray is not null)
        {
            // copy each member of old array2
            foreach (var index in Enumerable.Range(0, (int)partsList->PartCount))
                Buffer.MemoryCopy(oldPartArray + index, newArray + index, sizeof(AtkUldPart), sizeof(AtkUldPart));

            // free old array
            IMemorySpace.Free(oldPartArray, (ulong)sizeof(AtkUldPart) * partsList->PartCount);
        }

        // add new part
        Buffer.MemoryCopy(part, newArray + (newSize - 1), sizeof(AtkUldPart), sizeof(AtkUldPart));
        partsList->Parts = newArray;
        partsList->PartCount = newSize;
    }

    public static void AddAsset(AtkUldPart* part, AtkUldAsset* asset)
    {
        part->UldAsset = asset;
    }

    public static void FreeImageNode(AtkImageNode* node)
    {
        node->AtkResNode.Destroy(false);
        IMemorySpace.Free(node, (ulong)sizeof(AtkImageNode));
    }

    public static void FreeTextNode(AtkTextNode* node)
    {
        node->AtkResNode.Destroy(false);
        IMemorySpace.Free(node, (ulong)sizeof(AtkTextNode));
    }

    public static void FreePartsList(AtkUldPartsList* partsList)
    {
        foreach (var index in Enumerable.Range(0, (int)partsList->PartCount))
        {
            var part = &partsList->Parts[index];

            FreeAsset(part->UldAsset);
            FreePart(part);
        }

        IMemorySpace.Free(partsList, (ulong)sizeof(AtkUldPartsList));
    }

    public static void FreePart(AtkUldPart* part)
    {
        IMemorySpace.Free(part, (ulong)sizeof(AtkUldPart));
    }

    public static void FreeAsset(AtkUldAsset* asset)
    {
        IMemorySpace.Free(asset, (ulong)sizeof(AtkUldAsset));
    }

    public static void LinkNodeAtEnd(AtkResNode* imageNode, AtkUnitBase* parent)
    {
        var node = parent->RootNode->ChildNode;
        while (node->PrevSiblingNode != null) node = node->PrevSiblingNode;

        node->PrevSiblingNode = imageNode;
        imageNode->NextSiblingNode = node;
        imageNode->ParentNode = node->ParentNode;

        parent->UldManager.UpdateDrawNodeList();
    }

    public static void UnlinkNode<T>(T* atkNode, AtkComponentNode* componentNode) where T : unmanaged
    {
        var node = (AtkResNode*)atkNode;
        if (node == null) return;

        if (node->ParentNode->ChildNode == node) node->ParentNode->ChildNode = node->NextSiblingNode;

        if (node->NextSiblingNode != null && node->NextSiblingNode->PrevSiblingNode == node)
            node->NextSiblingNode->PrevSiblingNode = node->PrevSiblingNode;

        if (node->PrevSiblingNode != null && node->PrevSiblingNode->NextSiblingNode == node)
            node->PrevSiblingNode->NextSiblingNode = node->NextSiblingNode;

        componentNode->Component->UldManager.UpdateDrawNodeList();
    }

    public static void UnlinkAndFreeImageNode(AtkImageNode* node, AtkUnitBase* parent)
    {
        if (node->AtkResNode.PrevSiblingNode is not null)
            node->AtkResNode.PrevSiblingNode->NextSiblingNode = node->AtkResNode.NextSiblingNode;

        if (node->AtkResNode.NextSiblingNode is not null)
            node->AtkResNode.NextSiblingNode->PrevSiblingNode = node->AtkResNode.PrevSiblingNode;

        parent->UldManager.UpdateDrawNodeList();

        FreePartsList(node->PartsList);
        FreeImageNode(node);
    }

    public static void UnlinkAndFreeTextNode(AtkTextNode* node, AtkUnitBase* parent)
    {
        if (node->AtkResNode.PrevSiblingNode is not null)
            node->AtkResNode.PrevSiblingNode->NextSiblingNode = node->AtkResNode.NextSiblingNode;

        if (node->AtkResNode.NextSiblingNode is not null)
            node->AtkResNode.NextSiblingNode->PrevSiblingNode = node->AtkResNode.PrevSiblingNode;

        parent->UldManager.UpdateDrawNodeList();
        FreeTextNode(node);
    }
    #endregion

}
