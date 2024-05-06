using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using DailyRoutines.Helpers;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using DailyRoutines.Notifications;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;
using ECommons.Automation;
using ECommons.Throttlers;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoNotifySPPlayersTitle", "AutoNotifySPPlayersDescription", ModuleCategories.通知)]
public class AutoNotifySPPlayers : DailyModuleBase
{
    private class NotifiedPlayers
    {
        public string Name { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public HashSet<uint> Zone { get; set; } = [];
        public HashSet<uint> OnlineStatus { get; set; } = [];
    }

    private class Config : ModuleConfiguration
    {
        public readonly List<NotifiedPlayers> NotifiedPlayers = [];
    }

    private static Config ModuleConfig = null!;

    private static Dictionary<uint, OnlineStatus>? OnlineStatus;

    private static string ZoneSearchInput = string.Empty;

    private static readonly HashSet<uint> SelectedOnlineStatus = [];
    private static readonly HashSet<uint> SelectedZone = [];
    private static string SelectName = string.Empty;
    private static string SelectCommand = string.Empty;


    public override void Init()
    {
        OnlineStatus ??= LuminaCache.Get<OnlineStatus>().Where(x => x.RowId != 0 && x.RowId != 47)
                                    .ToDictionary(x => x.RowId, x => x);

        ModuleConfig = LoadConfig<Config>() ?? new();
        Service.FrameworkManager.Register(OnUpdate);
    }

    public override void ConfigUI()
    {
        var tableSize = new Vector2(ImGui.GetContentRegionAvail().X / 4 * 3, 0);
        if (ImGui.BeginTable("###AutoNotifySPPlayersTable", 2, ImGuiTableFlags.None, tableSize))
        {
            ImGui.TableSetupColumn("Lable", ImGuiTableColumnFlags.WidthStretch, 10);
            ImGui.TableSetupColumn("Content", ImGuiTableColumnFlags.WidthStretch, 60);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Text($"{Service.Lang.GetText("Name")}:");

            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1f);
            ImGui.InputTextWithHint("###NameInput", Service.Lang.GetText("AutoNotifySPPlayers-NameInputHint"), ref SelectName, 64);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Text($"{Service.Lang.GetText("AutoNotifySPPlayers-OnlineStatus")}:");

            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1f);
            if (ImGui.BeginCombo("###OnlineStatusCombo", Service.Lang.GetText("AutoNotifySPPlayers-SelectedEntryAmount", SelectedOnlineStatus.Count), ImGuiComboFlags.HeightLarge))
            {
                foreach (var statusPair in OnlineStatus)
                {
                    ImGui.PushID($"{statusPair.Value.Name.RawString}_{statusPair.Value.RowId}");
                    if (ImGuiOm.SelectableImageWithText(ImageHelper.GetIcon(statusPair.Value.Icon).ImGuiHandle,
                                                        ImGuiHelpers.ScaledVector2(20f),
                                                        statusPair.Value.Name.RawString,
                                                        SelectedOnlineStatus.Contains(statusPair.Key),
                                                        ImGuiSelectableFlags.DontClosePopups))
                    {
                        if (!SelectedOnlineStatus.Remove(statusPair.Key))
                            SelectedOnlineStatus.Add(statusPair.Key);
                    }

                    ImGui.PopID();
                }

                ImGui.EndCombo();
            }

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Text($"{Service.Lang.GetText("Zone")}:");

            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1f);
            if (ImGui.BeginCombo("###ZoneCombo", Service.Lang.GetText("AutoNotifySPPlayers-SelectedEntryAmount", SelectedZone.Count), ImGuiComboFlags.HeightLarge))
            {
                ImGui.InputTextWithHint("###ZoneSearchInput", Service.Lang.GetText("PleaseSearch"),
                                        ref ZoneSearchInput, 32);

                ImGui.Separator();

                foreach (var zonePair in PresetData.Zones)
                {
                    var zoneName = zonePair.Value.PlaceName.Value.Name.RawString;
                    if (!string.IsNullOrWhiteSpace(ZoneSearchInput) && !zoneName.Contains(ZoneSearchInput))
                        continue;

                    ImGui.PushID($"{zonePair.Value.Name.RawString}_{zonePair.Value.RowId}");
                    if (ImGuiOm.Selectable(zoneName, SelectedZone.Contains(zonePair.Key),
                                           ImGuiSelectableFlags.DontClosePopups))
                    {
                        if (!SelectedZone.Remove(zonePair.Key))
                            SelectedZone.Add(zonePair.Key);
                    }

                    ImGui.PopID();
                }

                ImGui.EndCombo();
            }

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Text($"{Service.Lang.GetText("AutoNotifySPPlayers-ExtraCommand")}:");

            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1f);
            ImGui.InputTextWithHint("###CommandInput", Service.Lang.GetText("AutoNotifySPPlayers-ExtraCommandInputHint"), ref SelectCommand, 64);

            ImGui.EndTable();
        }

        ImGui.SameLine();
        if (ImGuiOm.ButtonIconWithTextVertical(FontAwesomeIcon.Plus, Service.Lang.GetText("Add")))
        {
            if (!(string.IsNullOrWhiteSpace(SelectName) &&
                  SelectedOnlineStatus.Count <= 0 && SelectedZone.Count <= 0))
            {
                var preset = new NotifiedPlayers
                {
                    Name = SelectName,
                    OnlineStatus = SelectedOnlineStatus,
                    Zone = SelectedZone,
                    Command = SelectCommand
                };
                ModuleConfig.NotifiedPlayers.Add(preset);
                SaveConfig(ModuleConfig);
            }
        }

        ImGui.Separator();
        ImGui.Separator();

        foreach (var config in ModuleConfig.NotifiedPlayers.ToArray())
        {
            ImGui.Selectable(Service.Lang.GetText("AutoNotifySPPlayers-DisplayInfo", config.Name,
                                                  string.Join(",", config.OnlineStatus.Take(3)),
                                                  string.Join(",", config.Zone.Take(3)), config.Command));

            if (ImGui.BeginPopupContextItem(
                    $"{config.Name}-{string.Join(",", config.OnlineStatus.Take(3))}-{string.Join(",", config.Zone.Take(3))}-{config.Command}"))
            {
                if (ImGuiOm.ButtonSelectable($"{Service.Lang.GetText("Delete")}"))
                {
                    ModuleConfig.NotifiedPlayers.Remove(config);
                    SaveConfig(ModuleConfig);
                }

                ImGui.EndPopup();
            }

            ImGui.Separator();
        }
    }

    private static void OnUpdate(IFramework framework)
    {
        if (!EzThrottler.Throttle("AutoNotifySPPlayers", 1000) ||
            ModuleConfig.NotifiedPlayers.Count == 0 ||
            Service.ClientState.LocalPlayer == null)
            return;

        foreach (var player in Service.ObjectTable)
        {
            if (player.ObjectKind != ObjectKind.Player || player is not Character chara)
                continue;

            foreach (var config in ModuleConfig.NotifiedPlayers)
            {
                bool[] checks = [true, true, true];

                if (!string.IsNullOrWhiteSpace(config.Name))
                {
                    try
                    {
                        checks[0] = config.Name.StartsWith('/')
                                        ? new Regex(config.Name).IsMatch(player.Name.TextValue)
                                        : player.Name.TextValue == config.Name;
                    }
                    catch (ArgumentException)
                    {
                        checks[0] = false;
                    }
                }

                if (config.OnlineStatus.Count > 0) checks[1] = config.OnlineStatus.Contains(chara.OnlineStatus.Id);

                if (config.Zone.Count > 0) checks[2] = config.Zone.Contains(Service.ClientState.TerritoryType);

                if (checks.All(x => x))
                {
                    var message = Service.Lang.GetText("AutoNotifySPPlayers-NoticeMessage", player.Name.TextValue);
                    Service.Chat.Print(message);
                    WinToast.Notify(message, message);

                    if (!string.IsNullOrWhiteSpace(config.Command)) Chat.Instance.SendMessage(config.Command);
                }
            }
        }
    }
}
