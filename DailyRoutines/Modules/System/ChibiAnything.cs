using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Hooking;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using ImGuiNET;
using static DailyRoutines.Modules.CustomizeInterfaceText;
using CharacterStruct = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;
using GameObjectStruct = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace DailyRoutines.Modules;

[ModuleDescription("ChibiAnythingTitle", "ChibiAnythingDescription", ModuleCategories.系统)]
public unsafe class ChibiAnything : DailyModuleBase
{
    private struct ChibiTarget
    {
        public CustomizeType Type;
        public string Value;
        public float Scale;
    }

    private class CustomizePreset : IComparable<CustomizePreset>, IEquatable<CustomizePreset>
    {
        public CustomizeType Type { get; set; }
        public string Value { get; set; } = string.Empty;
        public float Scale { get; set; }
        public bool Enabled { get; set; }

        public CustomizePreset() { }

        public int CompareTo(CustomizePreset? other)
        {
            if (other == null) return 1;

            var typeComparison = Type.CompareTo(other.Type);
            if (typeComparison != 0) return typeComparison;

            var valueComparison = string.Compare(Value, other.Value, StringComparison.Ordinal);
            if (valueComparison != 0) return valueComparison;

            return Scale.CompareTo(other.Scale);
        }

        public override bool Equals(object? obj)
        {
            if (obj is CustomizePreset other)
            {
                return Equals(other);
            }
            return false;
        }

        public bool Equals(CustomizePreset? other)
        {
            if (other == null) return false;

            return Type == other.Type &&
                   string.Equals(Value, other.Value, StringComparison.Ordinal) &&
                   Scale.Equals(other.Scale);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Type, Value, Scale);
        }

        public static bool operator ==(CustomizePreset left, CustomizePreset right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(CustomizePreset left, CustomizePreset right)
        {
            return !Equals(left, right);
        }
    }

    private enum CustomizeType
    {
        名称,
        ModelCharaID,
        ModelSkeletonID,
        DataID,
        ObjectID
    }

    public override string? Author { get; set; } = "HSS";

    private class Config : ModuleConfiguration
    {
        public readonly List<CustomizePreset> CustomizePresets = [];

        public readonly List<ChibiTarget> TargetList = [];
    }

    private delegate byte IsTargetableDelegate(GameObjectStruct* gameObj);
    [Signature("40 53 48 83 EC 20 F3 0F 10 89 ?? ?? ?? ?? 0F 57 C0 0F 2E C8 48 8B D9 7A 0A",
               DetourName = nameof(IsTargetableDetour))]
    private static Hook<IsTargetableDelegate>? IsTargetableHook;

    private static Config ModuleConfig = null!;

    private static CustomizeType TypeInput = CustomizeType.名称;
    private static float ScaleInput = 1f;
    private static string ValueInput = "";

    private static int TypeEditInput;
    private static float ScaleEditInput = 1f;
    private static string ValueEditInput = "";

    private static bool IsTargetInfoWindowOpen;

    private static readonly Dictionary<nint, (CustomizePreset Preset, float Scale)> CustomizeHistory = [];

    public override void Init()
    {
        ModuleConfig = LoadConfig<Config>() ?? new();

        Service.Hook.InitializeFromAttributes(this);
        IsTargetableHook?.Enable();

        Service.ClientState.TerritoryChanged += OnZoneChanged;
    }

    public override void ConfigUI()
    {
        var tableSize0 = (ImGui.GetContentRegionAvail() / 3) with { Y = 0 };
        if (ImGui.BeginTable("NewConfigInputTable", 2, ImGuiTableFlags.None, tableSize0))
        {
            ImGui.TableSetupColumn("Lable", ImGuiTableColumnFlags.None, 30);
            ImGui.TableSetupColumn("Input", ImGuiTableColumnFlags.None, 70);

            // 类型
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.Text($"{Service.Lang.GetText("ChibiAnything-Config-chibiType")}:");

            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1f);
            if (ImGui.BeginCombo("###ChibiTypeCombo", TypeInput.ToString()))
            {
                foreach (var mode in Enum.GetValues<CustomizeType>())
                    if (ImGui.Selectable(mode.ToString(), mode == TypeInput))
                        TypeInput = mode;

                ImGui.EndCombo();
            }

            // 值
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.Text($"{Service.Lang.GetText("ChibiAnything-Config-value")}:");

            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1f);
            ImGui.InputText("###ChibiAnything-ValueConfig", ref ValueInput, 100);

            // 缩放
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.Text($"{Service.Lang.GetText("ChibiAnything-Config-scale")}:");

            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1f);
            ImGui.SliderFloat("###ChibiAnything-ScaleConfig", ref ScaleInput, 0.1f, 10f, "%.1f");

            ImGui.EndTable();
        }

        ImGui.SameLine();
        if (ImGuiOm.ButtonIconWithTextVertical(FontAwesomeIcon.Plus, Service.Lang.GetText("Add")))
        {
            if (ScaleInput > 0 && !string.IsNullOrWhiteSpace(ValueInput))
            {
                ModuleConfig.CustomizePresets.Add(
                    new CustomizePreset { Enabled = true, Scale = ScaleInput, Type = TypeInput, Value = ValueInput });
                SaveConfig(ModuleConfig);
            }
        }

        ImGui.SameLine();
        if (ImGuiOm.ButtonIconWithTextVertical(FontAwesomeIcon.Info, "目标信息"))
            IsTargetInfoWindowOpen ^= true;

        if (IsTargetInfoWindowOpen && ImGui.Begin("目标信息预览###TargetInfoPreviewWindow", 
                                                  ImGuiWindowFlags.NoDocking))
        {
            if (ImGui.IsWindowAppearing() && ImGui.GetWindowSize().X < 100f)
                ImGui.SetWindowSize(ImGuiHelpers.ScaledVector2(400f, 150f));
            var currentTarget = TargetSystem.Instance()->Target;
            if (currentTarget != null && currentTarget->IsCharacter())
            {
                ImGui.SameLine();

                var tableSize1 = ImGui.GetContentRegionAvail() with { Y = 0 };
                if (ImGui.BeginTable("TargetInfoPreviewTable", 2, ImGuiTableFlags.None, tableSize1))
                {
                    ImGui.TableSetupColumn("Lable", ImGuiTableColumnFlags.None, 30);
                    ImGui.TableSetupColumn("Input", ImGuiTableColumnFlags.None, 70);

                    // Target Name
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text("名称:");

                    ImGui.TableNextColumn();
                    var targetName = Marshal.PtrToStringUTF8((nint)currentTarget->Name);
                    ImGui.SetNextItemWidth(-1f);
                    ImGui.InputText("###TargetNamePreview", ref targetName, 128, ImGuiInputTextFlags.ReadOnly);

                    // Data ID
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text("Data ID:");

                    ImGui.TableNextColumn();
                    var targetDataID = currentTarget->DataID.ToString();
                    ImGui.SetNextItemWidth(-1f);
                    ImGui.InputText("###TargetDataIDPreview", ref targetDataID, 128, ImGuiInputTextFlags.ReadOnly);

                    // Object ID
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text("Object ID:");

                    ImGui.TableNextColumn();
                    var targetObjectID = currentTarget->ObjectID.ToString();
                    ImGui.SetNextItemWidth(-1f);
                    ImGui.InputText("###TargetObjectIDPreview", ref targetObjectID, 128, ImGuiInputTextFlags.ReadOnly);

                    // ModelChara ID
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text("Model Chara ID:");

                    ImGui.TableNextColumn();
                    var targetModelCharaID = ((CharacterStruct*)currentTarget)->CharacterData.ModelCharaId.ToString();
                    ImGui.SetNextItemWidth(-1f);
                    ImGui.InputText("###TargetModelCharaIDPreview", ref targetModelCharaID, 128, ImGuiInputTextFlags.ReadOnly);

                    // ModelChara ID
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text("Model Skeleton ID:");

                    ImGui.TableNextColumn();
                    var targetSkeletonID = ((CharacterStruct*)currentTarget)->CharacterData.ModelSkeletonId.ToString();
                    ImGui.SetNextItemWidth(-1f);
                    ImGui.InputText("###TargetModelSkeletonIDPreview", ref targetSkeletonID, 128, ImGuiInputTextFlags.ReadOnly);

                    ImGui.EndTable();
                }
            }

            ImGui.End();
        }

        ImGui.Separator();

        var tableSize2 = ImGui.GetContentRegionAvail() with { Y = 0 };
        if (ImGui.BeginTable("###ConfigTable", 5, ImGuiTableFlags.BordersInner, tableSize2))
        {
            ImGui.TableSetupColumn("启用", ImGuiTableColumnFlags.WidthFixed, 20 * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("模式", ImGuiTableColumnFlags.None, 20);
            ImGui.TableSetupColumn("值", ImGuiTableColumnFlags.None, 40);
            ImGui.TableSetupColumn("缩放比例", ImGuiTableColumnFlags.None, 15);
            ImGui.TableSetupColumn("操作", ImGuiTableColumnFlags.None, 20);

            ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
            ImGui.TableNextColumn();
            ImGui.Text("");
            ImGui.TableNextColumn();
            ImGui.Text("模式");
            ImGui.TableNextColumn();
            ImGui.Text("值");
            ImGui.TableNextColumn();
            ImGui.Text("缩放比例");
            ImGui.TableNextColumn();
            ImGui.Text("");

            var array = ModuleConfig.CustomizePresets.ToArray();
            for (var i = 0; i < ModuleConfig.CustomizePresets.Count; i++)
            {
                var preset = array[i];

                ImGui.PushID($"Preset_{i}");

                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                var isEnabled = preset.Enabled;
                if (ImGui.Checkbox("###IsEnabled", ref isEnabled))
                {
                    ModuleConfig.CustomizePresets[i].Enabled = isEnabled;
                    SaveConfig(ModuleConfig);

                    var match = CustomizeHistory.FirstOrDefault(x => x.Value.Preset == preset);
                    if (!isEnabled && match.Value.Preset != null)
                    {
                        ResetCustomizeFromHistory(match.Key);
                        CustomizeHistory.Remove(match.Key);
                    }
                }

                ImGui.TableNextColumn();
                ImGui.Selectable(preset.Type.ToString());

                if (ImGui.BeginPopupContextItem())
                {
                    if (ImGui.IsWindowAppearing())
                        TypeEditInput = (int)preset.Type;

                    ImGui.AlignTextToFramePadding();
                    ImGui.Text("匹配类型:");

                    foreach (var customizeType in Enum.GetValues<CustomizeType>())
                    {
                        ImGui.SameLine();
                        if (ImGui.RadioButton(customizeType.ToString(), ref TypeEditInput, (int)customizeType))
                        {
                            ModuleConfig.CustomizePresets[i].Type = (CustomizeType)TypeEditInput;
                            SaveConfig(ModuleConfig);
                        }
                    }

                    ImGui.EndPopup();
                }

                ImGui.TableNextColumn();
                ImGui.Selectable(preset.Value);

                if (ImGui.BeginPopupContextItem())
                {
                    if (ImGui.IsWindowAppearing())
                        ValueEditInput = preset.Value;

                    ImGui.AlignTextToFramePadding();
                    ImGui.Text("值:");

                    ImGui.SameLine();
                    ImGui.InputText("###ValueEditInput", ref ValueEditInput, 128);

                    if (ImGui.IsItemDeactivatedAfterEdit())
                    {
                        ModuleConfig.CustomizePresets[i].Value = ValueEditInput;
                        SaveConfig(ModuleConfig);
                    }

                    ImGui.EndPopup();
                }

                ImGui.TableNextColumn();
                ImGui.Selectable(preset.Scale.ToString(CultureInfo.InvariantCulture));

                if (ImGui.BeginPopupContextItem())
                {
                    if (ImGui.IsWindowAppearing())
                        ScaleEditInput = preset.Scale;

                    ImGui.AlignTextToFramePadding();
                    ImGui.Text("缩放比例:");

                    ImGui.SameLine();
                    ImGui.SliderFloat("###ScaleEditInput", ref ScaleEditInput, 0.1f, 10f, "%.1f");

                    if (ImGui.IsItemDeactivatedAfterEdit())
                    {
                        ModuleConfig.CustomizePresets[i].Scale = ScaleEditInput;
                        SaveConfig(ModuleConfig);
                    }

                    ImGui.EndPopup();
                }

                ImGui.TableNextColumn();
                if (ImGuiOm.ButtonIcon($"DeletePreset_{i}", FontAwesomeIcon.TrashAlt, "按住 Ctrl 以删除"))
                {
                    ModuleConfig.CustomizePresets.Remove(preset);
                    SaveConfig(ModuleConfig);
                }

                ImGui.SameLine();
                if (ImGuiOm.ButtonIcon($"ExportPreset_{i}", FontAwesomeIcon.FileExport, "导出至剪贴板"))
                    ExportToClipboard(preset);

                ImGui.SameLine();
                if (ImGuiOm.ButtonIcon($"ImportPreset_{i}", FontAwesomeIcon.FileImport, "从剪贴板导入"))
                {
                    var presetImport = ImportFromClipboard<CustomizePreset>();

                    if (presetImport != null && !ModuleConfig.CustomizePresets.Contains(presetImport))
                    {
                        ModuleConfig.CustomizePresets.Add(presetImport);
                        SaveConfig(ModuleConfig);
                    }
                }

                ImGui.PopID();
            }


            ImGui.EndTable();
        }
    }

    private static byte IsTargetableDetour(GameObjectStruct* pTarget)
    {
        var isTargetable = IsTargetableHook.Original(pTarget);

        if (ModuleConfig.CustomizePresets.Count == 0 || !pTarget->IsCharacter()) return isTargetable;

        var name = Marshal.PtrToStringUTF8((nint)pTarget->Name);
        var dataID = pTarget->DataID;
        var objectID = pTarget->ObjectID;
        var charaData = ((CharacterStruct*)pTarget)->CharacterData;
        var modelCharaID = charaData.ModelCharaId;
        var modelSkeletonID = charaData.ModelSkeletonId;

        foreach (var preset in ModuleConfig.CustomizePresets)
        {
            if (!preset.Enabled) continue;

            var isNeedToReScale = false;
            switch (preset.Type)
            {
                case CustomizeType.名称:
                    if (name.Equals(preset.Value)) isNeedToReScale = true;
                    break;
                case CustomizeType.DataID:
                    if (dataID.ToString() == preset.Value) isNeedToReScale = true;
                    break;
                case CustomizeType.ObjectID:
                    if (objectID.ToString() == preset.Value) isNeedToReScale = true;
                    break;
                case CustomizeType.ModelCharaID:
                    if (modelCharaID.ToString() == preset.Value)
                    {
                        isNeedToReScale = true;
                        charaData.ModelScale = preset.Scale;
                    }
                    break;
                case CustomizeType.ModelSkeletonID:
                    if (modelSkeletonID.ToString() == preset.Value)
                    {
                        isNeedToReScale = true;
                        charaData.ModelScale = preset.Scale;
                    }
                    break;
            }

            if (isNeedToReScale && pTarget->Scale != preset.Scale)
            {
                CustomizeHistory.TryAdd((nint)pTarget, (preset, pTarget->Scale));

                pTarget->Scale = preset.Scale;
                pTarget->VfxScale = preset.Scale;
                pTarget->DisableDraw();
                pTarget->EnableDraw();
            }
        }

        return 1;
    }

    private static void OnZoneChanged(ushort zone)
    {
        CustomizeHistory.Clear();
    }

    private static void ResetCustomizeFromHistory(nint address)
    {
        if (CustomizeHistory.Count == 0) return;

        if (!CustomizeHistory.TryGetValue(address, out var data)) return;

        var gameObj = (GameObjectStruct*)address;
        if (gameObj == null || !gameObj->IsReadyToDraw()) return;

        gameObj->Scale = data.Scale;
        gameObj->DisableDraw();
        gameObj->EnableDraw();
    }

    private static void ResetAllCustomizeFromHistory()
    {
        if (CustomizeHistory.Count == 0) return;

        foreach (var (objectPtr, data) in CustomizeHistory)
        {
            var gameObj = (GameObjectStruct*)objectPtr;
            if (gameObj == null || !gameObj->IsReadyToDraw()) continue;

            gameObj->Scale = data.Scale;
            gameObj->DisableDraw();
            gameObj->EnableDraw();
        }
    }

    public override void Uninit()
    {
        Service.ClientState.TerritoryChanged -= OnZoneChanged;

        ResetAllCustomizeFromHistory();
        CustomizeHistory.Clear();

        base.Uninit();
    }
}
