using System;
using System.Numerics;
using System.Runtime.InteropServices;
using DailyRoutines.Managers;
using Dalamud.Memory;

namespace DailyRoutines.Helpers;

public class FieldMarkerHelper
{
    public delegate bool RemoveFieldMarkerDelegate(long a1, uint index);
    public static RemoveFieldMarkerDelegate? RemoveFieldMarker;

    public static nint FieldMarkerData;
    public static nint FieldMarkerController;

    public enum FieldMarkerPoint
    {
        A, B, C, D, One, Two, Three, Four
    }

    public static void Init()
    {
        FieldMarkerController = Service.SigScanner.GetStaticAddressFromSig("48 8D 0D ?? ?? ?? ?? 41 B0 ?? E8 ?? ?? ?? ?? 85 C0");
        FieldMarkerData = FieldMarkerController + 0x1E0;

        RemoveFieldMarker ??= Marshal.GetDelegateForFunctionPointer<RemoveFieldMarkerDelegate>
            (Service.SigScanner.ScanText("E8 ?? ?? ?? ?? EB D8 83 FB 09"));
    }

    public static void Place(FieldMarkerPoint index, Vector3 pos, bool isActive)
    {
        var markAddress = index switch
        {
            FieldMarkerPoint.A => FieldMarkerData + 0x00,
            FieldMarkerPoint.B => FieldMarkerData + 0x20,
            FieldMarkerPoint.C => FieldMarkerData + 0x40,
            FieldMarkerPoint.D => FieldMarkerData + 0x60,
            FieldMarkerPoint.One => FieldMarkerData + 0x80,
            FieldMarkerPoint.Two => FieldMarkerData + 0xA0,
            FieldMarkerPoint.Three => FieldMarkerData + 0xC0,
            FieldMarkerPoint.Four => FieldMarkerData + 0xE0,
            _ => nint.Zero
        };

        MemoryHelper.Write(markAddress, pos.X);
        MemoryHelper.Write(markAddress + 0x4, pos.Y);
        MemoryHelper.Write(markAddress + 0x8, pos.Z);

        MemoryHelper.Write(markAddress + 0x10, (int)(pos.X * 1000));
        MemoryHelper.Write(markAddress + 0x14, (int)(pos.Y * 1000));
        MemoryHelper.Write(markAddress + 0x18, (int)(pos.Z * 1000));

        MemoryHelper.Write(markAddress + 0x1C, (byte)(isActive ? 1 : 0));
    }

    public static void Place(uint index, Vector3 pos, bool isActive)
    {
        if (index > 7) return;

        var markAddress = index switch
        {
            0 => FieldMarkerData + 0x00,
            1 => FieldMarkerData + 0x20,
            2 => FieldMarkerData + 0x40,
            3 => FieldMarkerData + 0x60,
            4 => FieldMarkerData + 0x80,
            5 => FieldMarkerData + 0xA0,
            6 => FieldMarkerData + 0xC0,
            7 => FieldMarkerData + 0xE0,
        };

        MemoryHelper.Write(markAddress, pos.X);
        MemoryHelper.Write(markAddress + 0x4, pos.Y);
        MemoryHelper.Write(markAddress + 0x8, pos.Z);

        MemoryHelper.Write(markAddress + 0x10, (int)(pos.X * 1000));
        MemoryHelper.Write(markAddress + 0x14, (int)(pos.Y * 1000));
        MemoryHelper.Write(markAddress + 0x18, (int)(pos.Z * 1000));

        MemoryHelper.Write(markAddress + 0x1C, (byte)(isActive ? 1 : 0));
    }

    public static void Remove(FieldMarkerPoint index)
    {
        var markerIndex = index switch
        {
            FieldMarkerPoint.A => 0U,
            FieldMarkerPoint.B => 1U,
            FieldMarkerPoint.C => 2U,
            FieldMarkerPoint.D => 3U,
            FieldMarkerPoint.One => 4U,
            FieldMarkerPoint.Two => 5U,
            FieldMarkerPoint.Three => 6U,
            FieldMarkerPoint.Four => 7U,
            _ => 0U
        };

        RemoveFieldMarker(FieldMarkerController, markerIndex);
    }

    public static void Remove(uint index)
    {
        if (index > 7) return;
        RemoveFieldMarker(FieldMarkerController, index);
    }
}
