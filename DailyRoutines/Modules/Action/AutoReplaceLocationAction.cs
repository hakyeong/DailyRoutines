using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DailyRoutines.Helpers;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;
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
public unsafe class AutoReplaceLocationAction : DailyModuleBase
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

    private delegate bool UseActionLocationDelegate(ActionManager* manager, ActionType type, uint actionID, ulong targetID, Vector3* location, uint a4);
    private Hook<UseActionLocationDelegate>? UseActionLocationHook;

    private static Config ModuleConfig = null!;

    private static uint CurrentMapID;
    private static readonly Dictionary<MapMarker, Vector2> ZoneMapMarkers = [];


    public override void Init()
    {
        ModuleConfig = LoadConfig<Config>() ?? new();

        Service.FrameworkManager.Register(OnUpdate);

        UseActionLocationHook = Service.Hook.HookFromAddress<UseActionLocationDelegate>((nint)ActionManager.MemberFunctionPointers.UseActionLocation, UseActionLocationDetour);
        UseActionLocationHook?.Enable();
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

    private bool UseActionLocationDetour(ActionManager* manager, ActionType type, uint actionID, ulong targetID, Vector3* location, uint a4)
    {
        if (type is ActionType.Action && ModuleConfig.EnabledActions.TryGetValue(actionID, out var isEnabled) && isEnabled)
        {
            var modifiedLocation = ZoneMapMarkers.Values.FirstOrDefault
                (x => Vector3.Distance(x.ToVector3(), Service.ClientState.LocalPlayer.Position) < 25)
                                                 .ToVector3();

            if (modifiedLocation.X != 0 && modifiedLocation.Z != 0)
            {
                if (Vector3.Distance(*location, modifiedLocation) < 15)
                {
                    var original = UseActionLocationHook.Original
                        (manager, type, actionID, targetID, &modifiedLocation, a4);
                    if (original && ModuleConfig.SendMessage)
                        Service.Chat.Print(new SeStringBuilder().Append(DRPrefix()).Append(Service.Lang.GetText("AutoReplaceLocationAction-RedirectMessage", modifiedLocation)).Build());
                    return original;
                }
            }
            else if (PresetData.TryGetContent(Service.ClientState.TerritoryType, out var content) &&
                     content.ContentType.Row is 4 or 5)
            {
                var map = LuminaCache.GetRow<Map>(CurrentMapID);
                modifiedLocation = MapHelper.MapToWorld(new(6.125f), map)
                                            .ToVector3();

                if (Vector3.Distance(*location, modifiedLocation) < 15)
                {
                    var original = UseActionLocationHook.Original
                        (manager, type, actionID, targetID, &modifiedLocation, a4);
                    if (original && ModuleConfig.SendMessage)
                        Service.Chat.Print(new SeStringBuilder().Append(DRPrefix()).Append(Service.Lang.GetText("AutoReplaceLocationAction-RedirectMessage", modifiedLocation)).Build());
                    return original;
                }
            }
        }

        return UseActionLocationHook.Original(manager, type, actionID, targetID, location, a4);
    }
}
