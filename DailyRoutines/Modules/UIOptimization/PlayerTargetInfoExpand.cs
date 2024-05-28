using System;
using System.Collections.Generic;
using System.Numerics;
using DailyRoutines.Helpers;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;

namespace DailyRoutines.Modules;

[ModuleDescription("PlayerTargetInfoExpandTitle", "PlayerTargetInfoExpandDescription", ModuleCategories.界面优化)]
public unsafe class PlayerTargetInfoExpand : DailyModuleBase
{
    private class Payload(string placeholder, string description, Func<Character, string> valueFunc)
    {
        public string Placeholder { get; } = placeholder;
        public string Description { get; } = description;
        public Func<Character, string> ValueFunc { get; } = valueFunc;
    }

    private static readonly List<Payload> Payloads =
    [
        new Payload("/Name/", Service.Lang.GetText("Name"), c => c.Name.TextValue),
        new Payload("/Job/", Service.Lang.GetText("Job"), c => c.ClassJob.GameData?.Name.RawString ?? LuminaCache.GetRow<ClassJob>(0).Name.RawString),
        new Payload("/Level/", Service.Lang.GetText("Level"), c => c.Level.ToString()),
        new Payload("/FCTag/", Service.Lang.GetText("PlayerTargetInfoExpand-CompanyTag"), c => c.CompanyTag.TextValue),
        new Payload("/OnlineStatus/", Service.Lang.GetText("PlayerTargetInfoExpand-OnlineStatus"),
                    c => string.IsNullOrWhiteSpace(c.OnlineStatus.GameData?.Name.RawString)
                             ? LuminaCache.GetRow<OnlineStatus>(47).Name.RawString
                             : c.OnlineStatus.GameData?.Name.RawString)
    ];

    private class Config : ModuleConfiguration
    {
        public string TargetPattern = "/Name/ [/Job/] «/FCTag/»";
        public string TargetsTargetPattern = "/Name/";
        public string FocusTargetPattern = "/Level/级 /Name/";
    }

    private static Config ModuleConfig = null!;

    public override void Init()
    {
        ModuleConfig = LoadConfig<Config>() ?? new();

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "_TargetInfo", UpdateTargetInfo);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "_TargetInfoMainTarget", UpdateTargetInfoMainTarget);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "_FocusTargetInfo", UpdateFocusTargetInfo);
    }

    public override void ConfigUI()
    {
        var tableSize = new Vector2(ImGui.GetContentRegionAvail().X / 2, 0);
        DrawInputAndPreviewText(Service.Lang.GetText("Target"), ref ModuleConfig.TargetPattern);
        DrawInputAndPreviewText(Service.Lang.GetText("PlayerTargetInfoExpand-TargetsTarget"), ref ModuleConfig.TargetsTargetPattern);
        DrawInputAndPreviewText(Service.Lang.GetText("PlayerTargetInfoExpand-FocusTarget"), ref ModuleConfig.FocusTargetPattern);

        if (ImGui.BeginTable("PayloadDisplay", 2, ImGuiTableFlags.Borders, tableSize))
        {
            ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
            ImGui.TableNextColumn();
            ImGui.Text("可用负载");
            ImGui.TableNextColumn();
            ImGui.Text("描述");

            foreach (var payload in Payloads)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(payload.Placeholder);
                ImGui.TableNextColumn();
                ImGui.Text(payload.Description);
            }

            ImGui.EndTable();
        }

        return;

        void DrawInputAndPreviewText(string categoryTitle, ref string config)
        {
            if (ImGui.BeginTable(categoryTitle, 2, ImGuiTableFlags.BordersOuter, tableSize))
            {
                ImGui.TableSetupColumn("###Category", ImGuiTableColumnFlags.None, 10);
                ImGui.TableSetupColumn("###Content", ImGuiTableColumnFlags.None, 50);

                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.Text($"{categoryTitle}:");

                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.InputText($"###{categoryTitle}", ref config, 64))
                    SaveConfig(ModuleConfig);

                if (Service.ClientState.LocalPlayer != null && Service.ClientState.LocalPlayer is Character chara)
                {
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text($"{Service.Lang.GetText("Example")}:");

                    ImGui.TableNextColumn();
                    ImGui.Text(ReplacePatterns(config, Payloads, chara));
                }

                ImGui.EndTable();
            }

            ImGui.Spacing();
        }
    }

    private static void UpdateTargetInfo(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)args.Addon;
        if (addon == null || !addon->IsVisible) return;

        // 目标
        var target = Service.Target.Target;
        var node0 = addon->GetTextNodeById(16);
        if (node0 != null && target is Character { ObjectKind: ObjectKind.Player } chara0)
            node0->SetText(ReplacePatterns(ModuleConfig.TargetPattern, Payloads, chara0));

        // 目标的目标
        var targetsTarget = Service.Target.Target?.TargetObject;
        var node1 = addon->GetTextNodeById(7);
        if (node1 != null && targetsTarget is Character { ObjectKind: ObjectKind.Player } chara1)
            node1->SetText(ReplacePatterns(ModuleConfig.TargetsTargetPattern, Payloads, chara1));
    }

    private static void UpdateTargetInfoMainTarget(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)args.Addon;
        if (addon == null || !addon->IsVisible) return;

        // 目标
        var target = Service.Target.Target;
        var node0 = addon->GetTextNodeById(10);
        if (node0 != null && target is Character { ObjectKind: ObjectKind.Player } chara0)
            node0->SetText(ReplacePatterns(ModuleConfig.TargetPattern, Payloads, chara0));

        // 目标的目标
        var targetsTarget = Service.Target.Target?.TargetObject;
        var node1 = addon->GetTextNodeById(7);
        if (node1 != null && targetsTarget is Character { ObjectKind: ObjectKind.Player } chara1)
            node1->SetText(ReplacePatterns(ModuleConfig.TargetsTargetPattern, Payloads, chara1));
    }

    private static void UpdateFocusTargetInfo(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)args.Addon;
        if (addon == null || !addon->IsVisible) return;

        // 焦点目标
        var target = Service.Target.FocusTarget;
        var node0 = addon->GetTextNodeById(10);
        if (node0 != null && target is Character { ObjectKind: ObjectKind.Player } chara0)
            node0->SetText(ReplacePatterns(ModuleConfig.FocusTargetPattern, Payloads, chara0));
    }

    private static string ReplacePatterns(string input, IEnumerable<Payload> payloads, Character chara)
    {
        foreach (var payload in payloads)
            input = input.Replace(payload.Placeholder, payload.ValueFunc(chara));
        return input;
    }

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnMainTarget);
        Service.AddonLifecycle.UnregisterListener(OnFocusTarget);

        base.Uninit();
    }
}
