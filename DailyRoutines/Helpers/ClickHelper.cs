using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace DailyRoutines.Helpers;

public unsafe class ClickHelper
{
    public static bool ContextMenu(IReadOnlyList<string> text)
    {
        if (!TryGetAddonByName<AtkUnitBase>("ContextMenu", out var addon) || !IsAddonAndNodesReady(addon)) return false;
        if (!TryScanContextMenuText(addon, text, out var index)) return false;
        
        return ContextMenu(index);
    }

    public static bool ContextMenu(string text)
    {
        if (!TryGetAddonByName<AtkUnitBase>("ContextMenu", out var addon) || !IsAddonAndNodesReady(addon)) return false;
        if (!TryScanContextMenuText(addon, text, out var index)) return false;
        
        return ContextMenu(index);
    }

    public static bool ContextMenu(int index)
    {
        if (!TryGetAddonByName<AtkUnitBase>("ContextMenu", out var addon) || !IsAddonAndNodesReady(addon)) return false;

        AddonHelper.Callback(addon, true, 0, index, 0U, 0, 0);
        return true;
    }

    public static bool SelectString(IReadOnlyList<string> text)
    {
        if (!TryGetAddonByName<AtkUnitBase>("SelectString", out var addon) || !IsAddonAndNodesReady(addon)) return false;
        if (!TryScanSelectStringText(addon, text, out var index)) return false;

        return SelectString(index);
    }

    public static bool SelectString(string text)
    {
        if (!TryGetAddonByName<AtkUnitBase>("SelectString", out var addon) || !IsAddonAndNodesReady(addon)) return false;
        if (!TryScanSelectStringText(addon, text, out var index)) return false;

        return SelectString(index);
    }

    public static bool SelectString(int index)
    {
        if (!TryGetAddonByName<AtkUnitBase>("SelectString", out var addon) || !IsAddonAndNodesReady(addon)) return false;

        AddonHelper.Callback(addon, true, index);
        return true;
    }

    public static bool SelectIconString(IReadOnlyList<string> text)
    {
        if (!TryGetAddonByName<AtkUnitBase>("SelectIconString", out var addon) || !IsAddonAndNodesReady(addon)) return false;
        if (!TryScanSelectIconStringText(addon, text, out var index)) return false;

        AddonHelper.Callback(addon, true, index);
        return true;
    }

    public static bool SelectIconString(string text)
    {
        if (!TryGetAddonByName<AtkUnitBase>("SelectIconString", out var addon) || !IsAddonAndNodesReady(addon)) return false;
        if (!TryScanSelectIconStringText(addon, text, out var index)) return false;

        AddonHelper.Callback(addon, true, index);
        return true;
    }

    public static bool SelectIconString(int index)
    {
        if (!TryGetAddonByName<AtkUnitBase>("SelectIconString", out var addon) || !IsAddonAndNodesReady(addon)) return false;

        AddonHelper.Callback(addon, true, index);
        return true;
    }
}
