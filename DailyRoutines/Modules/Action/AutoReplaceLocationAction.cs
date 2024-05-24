using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DailyRoutines.Helpers;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;
using ECommons.MathHelpers;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoReplaceLocationActionTitle", "AutoReplaceLocationActionDescription", ModuleCategories.技能)]
public class AutoReplaceLocationAction : DailyModuleBase
{
    private class Config : ModuleConfiguration
    {
        public readonly Dictionary<uint, bool> EnabledActions = new()
        {
            { 7439,  true }, // 地星
            { 25862, true }, // 礼仪之铃
            { 3569,  true }, // 庇护所
            { 188,   true }, // 野战治疗阵
        };

        public bool SendMessage = true;
    }

    private static Config ModuleConfig = null!;

    private static uint CurrentMapID;
    private static readonly Dictionary<MapMarker, Vector2> ZoneMapMarkers = [];
    private static Vector3? ModifiedLocation;


    public override void Init()
    {
        ModuleConfig = LoadConfig<Config>() ?? new();

        Service.FrameworkManager.Register(OnUpdate);
        Service.UseActionManager.Register(OnPreUseActionLocation);
        Service.UseActionManager.Register(OnPostUseActionLocation);
    }

    public override void ConfigUI()
    {
        ImGui.TextColored(ImGuiColors.DalamudOrange, $"{Service.Lang.GetText("WorkTheory")}:");
        ImGuiOm.HelpMarker(Service.Lang.GetText("AutoReplaceLocationAction-TheoryHelp"), 30f);

        ImGui.Checkbox(Service.Lang.GetText("AutoReplaceLocationAction-SendMessage"), ref ModuleConfig.SendMessage);

        var tableSize = new Vector2(ImGui.GetContentRegionAvail().X / 3, 0);
        if (ImGui.BeginTable("ActionEnableTable", 2, ImGuiTableFlags.Borders, tableSize))
        {
            foreach (var actionPair in ModuleConfig.EnabledActions)
            {
                var action = LuminaCache.GetRow<Action>(actionPair.Key);

                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                ImGuiOm.TextImage(action.Name.RawString, ImageHelper.GetIcon(action.Icon).ImGuiHandle,
                          ImGuiHelpers.ScaledVector2(20f));

                ImGui.TableNextColumn();
                var state = actionPair.Value;
                if (ImGui.Checkbox($"###{actionPair.Key}_{action.Name.RawString}", ref state))
                {
                    ModuleConfig.EnabledActions[actionPair.Key] = state;
                    SaveConfig(ModuleConfig);
                }
            }
            ImGui.EndTable();
        }
    }

    private static void OnUpdate(IFramework framework)
    {
        if (!EzThrottler.Throttle("AutoPlaceEarthlyStar", 1000)) return;
        if (!Flags.BoundByDuty()) return;

        if (CurrentMapID == Service.ClientState.MapId) return;

        CurrentMapID = Service.ClientState.MapId;
        var currentMap = LuminaCache.GetRow<Map>(CurrentMapID);

        ZoneMapMarkers.Clear();
        MapHelper.GetMapMarkers(CurrentMapID)
                 .ForEach(x =>
                 {
                     if (x.Icon == 60442)
                         ZoneMapMarkers.TryAdd(x, MapHelper.TextureToWorld(x.GetPosition(), currentMap));
                 });
    }

    private static void OnPreUseActionLocation(ref bool isPrevented, ref ActionType type, ref uint actionID,
                                               ref ulong targetID, ref Vector3 location, ref uint a4)
    {
        if (type != ActionType.Action || !ModuleConfig.EnabledActions.TryGetValue(actionID, out bool isEnabled) || !isEnabled)
            return;

        var resultLocation = ZoneMapMarkers.Values
                                           .Select(x => x.ToVector3() as Vector3?)
                                           .FirstOrDefault(x => x.HasValue && Vector3.Distance(x.Value, Service.ClientState.LocalPlayer.Position) < 25);

        if (resultLocation != null)
            UpdateLocationIfClose(ref location, resultLocation.Value, 15);
        else
            HandleAlternativeLocation(ref location);
    }

    private static void UpdateLocationIfClose(ref Vector3 currentLocation, Vector3 candidateLocation, float proximityThreshold)
    {
        if (Vector3.Distance(currentLocation, candidateLocation) < proximityThreshold)
        {
            currentLocation = candidateLocation;
            ModifiedLocation ??= candidateLocation;
        }
    }

    private static void HandleAlternativeLocation(ref Vector3 location)
    {
        if (PresetData.TryGetContent(Service.ClientState.TerritoryType, out var content) && 
            content.ContentType.Row is 4 or 5)
        {
            var map = LuminaCache.GetRow<Map>(CurrentMapID);
            var modifiedLocation = MapHelper.MapToWorld(new Vector2(6.125f), map).ToVector3();

            UpdateLocationIfClose(ref location, modifiedLocation, 15);
        }
    }

    private static void OnPostUseActionLocation(bool result, ActionType actionType, uint actionID, 
                                                ulong targetID, Vector3 location, uint a4)
    {
        if (!ModuleConfig.SendMessage || !result || ModifiedLocation == null) return;

        var message = new SeStringBuilder().Append(DRPrefix)
                                           .Append(Service.Lang.GetText("AutoReplaceLocationAction-RedirectMessage",
                                                                        ModifiedLocation))
                                           .Build();
        Service.Chat.Print(message);
        ModifiedLocation = null;
    }

    public override void Uninit()
    {
        Service.UseActionManager.Unregister(OnPreUseActionLocation);
        Service.UseActionManager.Unregister(OnPostUseActionLocation);
        ModifiedLocation = null;

        base.Uninit();
    }
}
