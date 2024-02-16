using System;
using System.Numerics;
using DailyRoutines.Managers;
using Dalamud.Memory;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace DailyRoutines.Manager;

public unsafe class FieldMarkerManager
{
    [Signature("E8 ?? ?? ?? ?? EB D8 83 FB 09")]
    public readonly delegate* unmanaged<long, uint, char> RemoveFieldMarkerOriginal;

    public nint FieldMarkerData;
    public nint FieldMarkerController;

    public FieldMarkerManager()
    {
        FieldMarkerController =
            Service.SigScanner.GetStaticAddressFromSig("48 8D 0D ?? ?? ?? ?? 41 B0 ?? E8 ?? ?? ?? ?? 85 C0");
        FieldMarkerData = FieldMarkerController + 0x1E0;

        SignatureHelper.Initialise(this);
    }

    public void Place(WaymarkIndex index, Vector3 pos, bool isActive)
    {
        var markAddress = index switch
        {
            WaymarkIndex.A => FieldMarkerData + 0x00,
            WaymarkIndex.B => FieldMarkerData + 0x20,
            WaymarkIndex.C => FieldMarkerData + 0x40,
            WaymarkIndex.D => FieldMarkerData + 0x60,
            WaymarkIndex.One => FieldMarkerData + 0x80,
            WaymarkIndex.Two => FieldMarkerData + 0xA0,
            WaymarkIndex.Three => FieldMarkerData + 0xC0,
            WaymarkIndex.Four => FieldMarkerData + 0xE0,
            _ => IntPtr.Zero
        };

        MemoryHelper.Write(markAddress, pos.X);
        MemoryHelper.Write(markAddress + 0x4, pos.Y);
        MemoryHelper.Write(markAddress + 0x8, pos.Z);

        MemoryHelper.Write(markAddress + 0x10, (int)(pos.X * 1000));
        MemoryHelper.Write(markAddress + 0x14, (int)(pos.Y * 1000));
        MemoryHelper.Write(markAddress + 0x18, (int)(pos.Z * 1000));

        MemoryHelper.Write(markAddress + 0x1C, (byte)(isActive ? 1 : 0));
    }

    public void Place(uint index, Vector3 pos, bool isActive)
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
            _ => FieldMarkerData
        };

        MemoryHelper.Write(markAddress, pos.X);
        MemoryHelper.Write(markAddress + 0x4, pos.Y);
        MemoryHelper.Write(markAddress + 0x8, pos.Z);

        MemoryHelper.Write(markAddress + 0x10, (int)(pos.X * 1000));
        MemoryHelper.Write(markAddress + 0x14, (int)(pos.Y * 1000));
        MemoryHelper.Write(markAddress + 0x18, (int)(pos.Z * 1000));

        MemoryHelper.Write(markAddress + 0x1C, (byte)(isActive ? 1 : 0));
    }

    public void Remove(WaymarkIndex index)
    {
        var markerIndex = index switch
        {
            WaymarkIndex.A => 0U,
            WaymarkIndex.B => 1U,
            WaymarkIndex.C => 2U,
            WaymarkIndex.D => 3U,
            WaymarkIndex.One => 4U,
            WaymarkIndex.Two => 5U,
            WaymarkIndex.Three => 6U,
            WaymarkIndex.Four => 7U,
            _ => 0U
        };

        RemoveFieldMarkerOriginal(FieldMarkerController, markerIndex);
    }

    public void Remove(uint index)
    {
        if (index > 7) return;
        RemoveFieldMarkerOriginal(FieldMarkerController, index);
    }

    public void Uninit() { }
}
