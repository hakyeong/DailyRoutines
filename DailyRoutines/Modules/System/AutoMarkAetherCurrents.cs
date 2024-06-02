using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DailyRoutines.Helpers;
using DailyRoutines.Managers;
using DailyRoutines.Windows;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;

namespace DailyRoutines.Modules;

// 硬编码领域大神
[ModuleDescription("AutoMarkAetherCurrentsTitle", "AutoMarkAetherCurrentsDescription", ModuleCategories.系统)]
public unsafe class AutoMarkAetherCurrents : DailyModuleBase
{
    // 当前: 6.0 版本
    private static readonly HashSet<uint> NewVersionZones = [956, 957, 958, 959, 960, 961];

    private static readonly HashSet<uint> ValidZones =
    [
        397, 398, 399, 400, 401, 612, 613, 614, 620, 621, 622,
        813, 814, 815, 816, 817, 818, 956, 957, 958, 959, 960, 961,
    ];

    private static readonly Dictionary<uint, HashSet<AetherCurrentRecord>> AetherCurrentsPresetData = new()
    {
        // 迷津
        {
            956, [
                new(956, 0, new(346.53f, 209.35f, -767.74f)),
                new(956, 1, new(748.56f, 106.71f, 66.76f)),
                new(956, 2, new(-547.73f, -18.02f, 661.87f)),
                new(956, 3, new(-128.07f, -20.52f, 676.72f)),
                new(956, 4, new(-316.28f, 79.76f, -395.31f)),
                new(956, 5, new(32.33f, 72.83f, -286.27f)),
                new(956, 6, new(497.11f, 73.42f, -267.23f)),
                new(956, 7, new(-176.38f, -10.10f, -242.24f)),
                new(956, 8, new(-505.14f, -21.82f, -122.60f)),
                new(956, 9, new(46.28f, -29.80f, 178.88f)),
            ]
        },
        // 萨维奈岛
        {
            957, [
                new(957, 0, new(-176.10f, 21.53f, 537.84f)),
                new(957, 1, new(-49.27f, 94.07f, -710.76f)),
                new(957, 2, new(118.47f, 4.93f, -343.87f)),
                new(957, 3, new(550.02f, 25.48f, -159.08f)),
                new(957, 4, new(303.92f, 0.28f, 473.66f)),
                new(957, 5, new(-479.44f, 72.90f, -561.81f)),
                new(957, 6, new(-114.46f, 87.09f, -288.29f)),
                new(957, 7, new(93.12f, 36.68f, -447.86f)),
                new(957, 8, new(294.40f, 4.10f, 425.12f)),
                new(957, 9, new(53.19f, 11.39f, 187.41f)),
            ]
        },
        // 加雷马
        {
            958, [
                new(958, 0, new(-184.22f, 31.94f, 423.61f)),
                new(958, 1, new(194.82f, -12.84f, 644.31f)),
                new(958, 2, new(83.09f, 1.53f, 102.02f)),
                new(958, 3, new(405.30f, -2.24f, 520.32f)),
                new(958, 4, new(-516.09f, 42.47f, 67.84f)),
                new(958, 5, new(382.18f, 25.90f, -482.20f)),
                new(958, 6, new(-602.03f, 34.32f, -325.85f)),
                new(958, 7, new(79.91f, 37.89f, -518.18f)),
                new(958, 8, new(134.93f, 14.40f, -172.25f)),
                new(958, 9, new(-144.92f, 17.58f, -420.52f)),
            ]
        },
        // 叹息海
        {
            959, [
                new(959, 0, new(42.59f, 124.01f, -167.03f)),
                new(959, 1, new(-482.74f, -154.95f, -595.71f)),
                new(959, 2, new(316.40f, -154.98f, -595.52f)),
                new(959, 3, new(29.10f, -47.74f, -550.41f)),
                new(959, 4, new(-128.00f, 66.35f, -68.24f)),
                new(959, 5, new(591.38f, 149.36f, 114.94f)),
                new(959, 6, new(388.36f, 99.92f, 306.07f)),
                new(959, 7, new(652.98f, -160.69f, -405.08f)),
                new(959, 8, new(-733.62f, -139.66f, -733.28f)),
                new(959, 9, new(21.71f, -133.50f, -385.73f)),
            ]
        },
        // 厄尔庇斯
        {
            961, [
                new(961, 0, new(628.24f, 8.32f, 107.90f)),
                new(961, 1, new(-754.75f, -36.01f, 411.13f)),
                new(961, 2, new(151.67f, 7.67f, 2.55f)),
                new(961, 3, new(-144.54f, -26.23f, 551.52f)),
                new(961, 4, new(-481.41f, -28.58f, 490.56f)),
                new(961, 5, new(-402.92f, 327.76f, -691.32f)),
                new(961, 6, new(-555.62f, 158.12f, 172.43f)),
                new(961, 7, new(-392.05f, 173.72f, -293.60f)),
                new(961, 8, new(-761.71f, 160.01f, -108.99f)),
                new(961, 9, new(-255.51f, 143.08f, -36.97f)),
            ]
        },
        // 天外天垓
        {
            960, [
                new(960, 0, new(-333.54f, 270.85f, -361.49f)),
                new(960, 1, new(13.12f, 275.57f, -756.40f)),
                new(960, 2, new(661.78f, 439.98f, 411.76f)),
                new(960, 3, new(539.27f, 438.00f, 239.40f)),
                new(960, 4, new(424.57f, 283.38f, -679.76f)),
                new(960, 5, new(-238.79f, 320.39f, -295.14f)),
                new(960, 6, new(-385.22f, 262.52f, -629.85f)),
                new(960, 7, new(751.88f, 439.98f, 357.90f)),
                new(960, 8, new(637.20f, 439.24f, 289.67f)),
                new(960, 9, new(567.50f, 440.93f, 402.14f)),
            ]
        },
    };

    private static readonly Dictionary<uint, HashSet<AetherCurrentRecord>> AetherCurrentsData = [];
    private static Dictionary<uint, HashSet<AetherCurrentRecord>> SelectedAetherCurrentsData = [];

    private static bool UseLocalMark = true;

    private static AtkUnitBase* AetherCurrentAddon => (AtkUnitBase*)Service.Gui.GetAddonByName("AetherCurrent");

    public override void Init()
    {
        var aetherCurrentsObjectID = LuminaCache.Get<EObjName>()
                                                .Where(x => x.Singular.RawString.Equals("风脉泉"))
                                                .Select(x => x.RowId).ToArray();

        var levelSheet = LuminaCache.Get<Level>();
        var indexTracker = new Dictionary<uint, HashSet<uint>>(); // Zone - Index
        foreach (var current in aetherCurrentsObjectID)
        {
            var foundData = levelSheet.FirstOrDefault(x => x.Object == current);
            if (foundData == null) continue;

            var zone = foundData.Territory.Row;
            if (NewVersionZones.Contains(zone)) continue;

            var pos = new Vector3(foundData.X, foundData.Y, foundData.Z);
            var index = 0U;

            indexTracker.TryAdd(zone, []);

            while (indexTracker[zone].Contains(index)) index++;
            indexTracker[zone].Add(index);

            AetherCurrentsData.TryAdd(zone, []);
            AetherCurrentsData[zone].Add(new(zone, index, pos));
        }

        foreach (var validZone in ValidZones) SelectedAetherCurrentsData.TryAdd(validZone, []);

        Overlay ??= new Overlay(this);
        Overlay.Flags |= ImGuiWindowFlags.NoMove;

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "AetherCurrent", OnAddon);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "AetherCurrent", OnAddon);
        Service.ClientState.TerritoryChanged += OnZoneChanged;
    }

    public override void OverlayPreDraw()
    {
        if (AetherCurrentAddon == null) Overlay.IsOpen = false;
    }

    public override void OverlayUI()
    {
        var pos = new Vector2(AetherCurrentAddon->GetX() + 6, AetherCurrentAddon->GetY() - ImGui.GetWindowSize().Y + 6);
        ImGui.SetWindowPos(pos);

        ImGui.BeginGroup();
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(ImGuiColors.DalamudOrange, $"{Service.Lang.GetText("Operation")}:");

        ImGui.SameLine();
        ImGui.Checkbox(Service.Lang.GetText("AutoMarkAetherCurrents-UseLocalMark"), ref UseLocalMark);
        ImGuiOm.TooltipHover(Service.Lang.GetText("AutoMarkAetherCurrents-UseLocalMarkHelp"));

        ImGui.Spacing();

        if (ImGuiOm.ButtonIconWithTextVertical(FontAwesomeIcon.Sync,
                                               Service.Lang.GetText("AutoMarkAetherCurrents-RefreshDisplay")))
            MarkAetherCurrents(Service.ClientState.TerritoryType, false, UseLocalMark);

        ImGuiOm.TooltipHover(Service.Lang.GetText("AutoMarkAetherCurrents-FieldMarkerHelp"), 25f);

        ImGui.SameLine();
        if (ImGuiOm.ButtonIconWithTextVertical(FontAwesomeIcon.Random,
                                               Service.Lang.GetText("AutoMarkAetherCurrents-DisplayLeftCurrents")))
            MarkAetherCurrents(Service.ClientState.TerritoryType, true, UseLocalMark);

        ImGuiOm.TooltipHover(Service.Lang.GetText("AutoMarkAetherCurrents-DisplayLeftCurrentsHelp"));

        if (ImGuiOm.ButtonIconWithTextVertical(FontAwesomeIcon.Eraser,
                                               Service.Lang.GetText("AutoMarkAetherCurrents-RemoveAllWaymarks")))
            for (var i = 0U; i < 8; i++)
                FieldMarkerHelper.RemoveLocal(i);

        ImGui.SameLine();
        if (ImGuiOm.ButtonIconWithTextVertical(FontAwesomeIcon.TrashAlt,
                                               Service.Lang.GetText("AutoMarkAetherCurrents-RemoveSelectedAC")))
            foreach (var zoneCurrents in SelectedAetherCurrentsData)
                zoneCurrents.Value.Clear();

        ImGui.EndGroup();

        ImGui.SameLine();
        ImGui.Dummy(ImGuiHelpers.ScaledVector2(3f));

        ImGui.SameLine();
        ImGui.BeginGroup();
        ImGui.TextColored(ImGuiColors.DalamudOrange,
                          $"{Service.Lang.GetText("AutoMarkAetherCurrents-ManuallySelectCurrent")}:");

        ImGuiOm.HelpMarker(Service.Lang.GetText("AutoMarkAetherCurrents-ManuallySelectCurrentHelp"), 25f);

        if (ImGui.BeginTabBar("AutoMarkAetherCurrent-ManuallySelect"))
        {
            var tabs = new[] { "3.0", "4.0", "5.0", "6.0" };

            foreach (var tab in tabs)
                if (AetherCurrentAddon->AtkValues[3].Int != Array.IndexOf(tabs, tab))
                    ImGui.SetTabItemClosed(tab);

            DisplayRegion("3.0", false, [397, 399, 401, 398, 400]);
            DisplayRegion("4.0", false, [612, 620, 621, 613, 614, 622]);
            DisplayRegion("5.0", false, [813, 816, 817, 815, 814, 818]);
            DisplayRegion("6.0", true, [956, 958, 961, 957, 959, 960]);

            ImGui.EndTabBar();
        }

        ImGui.EndGroup();

        return;

        void DisplayRegion(string tab, bool isNew, IReadOnlyCollection<uint> regions)
        {
            if (ImGui.BeginTabItem(tab))
            {
                DisplayHalf(isNew, regions.Take(3).ToArray());

                ImGui.SameLine();
                DisplayHalf(isNew, regions.Skip(3).ToArray());

                ImGui.EndTabItem();
            }
        }

        void DisplayHalf(bool isNew, uint[] half)
        {
            ImGui.BeginGroup();
            ImGui.BeginGroup();
            foreach (var region in half)
            {
                var zoneName = LuminaCache.GetRow<TerritoryType>(region).PlaceName.Value.Name.RawString;
                if (string.IsNullOrWhiteSpace(zoneName)) continue;

                ImGui.AlignTextToFramePadding();
                ImGui.Text(zoneName);
            }

            ImGui.EndGroup();

            ImGui.SameLine();

            ImGui.BeginGroup();
            foreach (var region in half)
                if (isNew) ManuallySelectGroupNew(region, ref SelectedAetherCurrentsData);
                else ManuallySelectGroupOld(region, ref SelectedAetherCurrentsData);

            ImGui.EndGroup();

            ImGui.EndGroup();
        }

        void ManuallySelectGroupNew(uint zoneID, ref Dictionary<uint, HashSet<AetherCurrentRecord>> selectedCurrents)
        {
            var zoneName = LuminaCache.GetRow<TerritoryType>(zoneID).PlaceName.Value.Name.RawString;
            if (string.IsNullOrWhiteSpace(zoneName)) return;

            if (!AetherCurrentsPresetData.TryGetValue(zoneID, out var data)) return;
            ImGui.PushID($"{zoneName}_{zoneID}");

            for (var i = 9; i >= 0; i--)
            {
                var aetherCurrent = data.FirstOrDefault(d => d.Index == i);
                if (aetherCurrent == null) break;

                var decoBool = selectedCurrents[zoneID].Contains(aetherCurrent);
                if (ImGui.Checkbox($"###{zoneName}{i}", ref decoBool))
                {
                    if (!selectedCurrents[zoneID].Remove(aetherCurrent))
                        selectedCurrents[zoneID].Add(aetherCurrent);
                }

                if (i != 0) ImGui.SameLine();
            }

            ImGui.PopID();
        }

        void ManuallySelectGroupOld(uint zoneID, ref Dictionary<uint, HashSet<AetherCurrentRecord>> selectedCurrents)
        {
            var zoneName = LuminaCache.GetRow<TerritoryType>(zoneID).PlaceName.Value.Name.RawString;
            if (string.IsNullOrWhiteSpace(zoneName)) return;

            if (!AetherCurrentsData.TryGetValue(zoneID, out var data)) return;
            ImGui.PushID(zoneName);
            for (var i = 3; i >= 0; i--)
            {
                var aetherCurrent = data.FirstOrDefault(d => d.Index == i);
                if (aetherCurrent == null) break;

                var decoBool = selectedCurrents[zoneID].Contains(aetherCurrent);
                if (ImGui.Checkbox($"###{zoneName}{i}", ref decoBool))
                {
                    if (!selectedCurrents[zoneID].Remove(aetherCurrent))
                        selectedCurrents[zoneID].Add(aetherCurrent);
                }

                if (i != 0) ImGui.SameLine();
            }

            ImGui.PopID();
        }
    }

    private void OnAddon(AddonEvent type, AddonArgs args)
    {
        Overlay.IsOpen = type switch
        {
            AddonEvent.PostSetup => true,
            AddonEvent.PreFinalize => false,
            _ => Overlay.IsOpen,
        };
    }

    private static void OnZoneChanged(ushort zoneID) { MarkAetherCurrents(zoneID, false, UseLocalMark); }

    private static void MarkAetherCurrents(ushort zoneID, bool isFirstPage = false, bool isLocal = true)
    {
        if (!ValidZones.Contains(zoneID)) return;
        var isNew = NewVersionZones.Contains(zoneID);

        _ = isNew
                ? AetherCurrentsPresetData.TryGetValue(zoneID, out var dataSet)
                : AetherCurrentsData.TryGetValue(zoneID, out dataSet);

        if (dataSet == null || dataSet.Count == 0) return;

        var indexesLength = Enum.GetValues(typeof(FieldMarkerHelper.FieldMarkerPoint)).Length;
        var currentZone = Service.ClientState.TerritoryType;

        var result = SelectedAetherCurrentsData.TryGetValue(currentZone, out var selectedResult) &&
                     selectedResult.Count != 0
                         ? selectedResult
                         : dataSet.Where(i => isFirstPage ? i.Index >= indexesLength : i.Index < indexesLength)
                                  .ToHashSet();


        var currentIndex = 0U;

        foreach (var point in result)
        {
            if (currentIndex >= indexesLength) break;

            var currentMarker = (FieldMarkerHelper.FieldMarkerPoint)currentIndex;

            if (isLocal) FieldMarkerHelper.PlaceLocal(currentMarker, point.Position, true);
            else FieldMarkerHelper.PlaceOnline(currentMarker, point.Position);

            currentIndex++;
        }

        if (currentIndex != 8)
        {
            for (; currentIndex < indexesLength; currentIndex++)
                if (isLocal) FieldMarkerHelper.PlaceLocal(currentIndex, Vector3.Zero, false);
                else FieldMarkerHelper.RemoveOnline(currentIndex);
        }
    }

    public override void Uninit()
    {
        Service.ClientState.TerritoryChanged -= OnZoneChanged;
        Service.AddonLifecycle.UnregisterListener(OnAddon);

        base.Uninit();
    }

    private record AetherCurrentRecord(uint Zone, uint Index, Vector3 Position);
}
