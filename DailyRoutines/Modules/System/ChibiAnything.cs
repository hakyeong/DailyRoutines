using System;
using System.Collections.Generic;
using DailyRoutines.Infos;
using Dalamud.Game.ClientState.Objects.Types;
using Character = Dalamud.Game.ClientState.Objects.Types.Character;
using CharacterStruct = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;
using GameObjectStruct = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;
using DailyRoutines.Managers;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("ChibiAnything", "ChibiAnythingDescription", ModuleCategories.系统)]
public class ChibiAnything : DailyModuleBase
{
    public override string? Author { get; set; } = "HSS";

    private class Config : ModuleConfiguration
    {
        public List<ChibiTarget> TargetList = new();
    }

    private struct ChibiTarget
    {
        public ChibiType Type;
        public string Value;
        public float Scale;
    }

    private enum ChibiType
    {
        ModelCharaId,
        Name,
        DataId,
        ObjectId,
    }

    private static readonly Dictionary<ChibiType, string> ChibiTypeLoc = new()
    {
        { ChibiType.ModelCharaId, Service.Lang.GetText("ChibiAnything-Config-chibiType-ModelCharaId") },
        { ChibiType.Name, Service.Lang.GetText("ChibiAnything-Config-chibiType-Name") },
        { ChibiType.DataId, Service.Lang.GetText("ChibiAnything-Config-chibiType-DataId") },
        { ChibiType.ObjectId, Service.Lang.GetText("ChibiAnything-Config-chibiType-ObjectId") }
    };

    private Config ModuleConfig = null!;
    private ChibiType TempChibiType = ChibiType.Name;
    private float TempScale = 1f;
    private string TempValue = "";

    public override void Init()
    {
        ModuleConfig = LoadConfig<Config>() ?? new();
        Service.PluginInterface.UiBuilder.Draw += Draw;
    }

    public override void ConfigUI()
    {
        ImGui.Text(Service.Lang.GetText("ChibiAnything-Config-chibiType"));
        ImGui.SameLine();
        ImGui.SetNextItemWidth(200f);
        if (ImGui.BeginCombo("###ChibiTypeCombo", ChibiTypeLoc[TempChibiType]))
        {
            foreach (var mode in ChibiTypeLoc)
                if (ImGui.Selectable(mode.Value, mode.Key == TempChibiType))
                {
                    TempChibiType = mode.Key;
                }

            ImGui.EndCombo();
        }

        ImGui.Text(Service.Lang.GetText("ChibiAnything-Config-value"));
        ImGui.SameLine();
        ImGui.SetNextItemWidth(300f);
        ImGui.InputText("###ChibiAnything-ValueConfig", ref TempValue, 100);
        ImGui.Text(Service.Lang.GetText("ChibiAnything-Config-scale"));
        ImGui.SameLine();
        ImGui.SetNextItemWidth(300f);
        ImGui.SliderFloat("###ChibiAnything-ScaleConfig", ref TempScale, 0.1f, 5f, "%.1f");
        if (ImGui.Button($"{Service.Lang.GetText("ChibiAnything-Add")}###ChibiAnyting-saveButton"))
        {
            ModuleConfig.TargetList.Add(
                new ChibiTarget() { Type = TempChibiType, Value = TempValue, Scale = TempScale });
            SaveConfig(ModuleConfig);
        }

        ImGui.Separator();
        if (ModuleConfig.TargetList.Count > 0)
        {
            for (int i = ModuleConfig.TargetList.Count - 1; i >= 0; i--)
            {
                var target = ModuleConfig.TargetList[i];
                ImGui.Text($"{ChibiTypeLoc[target.Type]} : {target.Value} -- {target.Scale * 100}%%");
                ImGui.SameLine();
                if (ImGui.Button($"{Service.Lang.GetText("ChibiAnything-Del")}###ChibiAnyting-DelButton{i}"))
                {
                    ModuleConfig.TargetList.RemoveAt(i);
                    SaveConfig(ModuleConfig);
                }
            }
        }
    }

    private unsafe void Draw()
    {
        if (ModuleConfig.TargetList.Count == 0) return;
        foreach (var gameObject in Service.ObjectTable)
        {
            if (gameObject is Character)
            {
                foreach (var chibiTarget in ModuleConfig.TargetList)
                {
                    try
                    {
                        var flag = false;
                        switch (chibiTarget.Type)
                        {
                            case ChibiType.Name:
                                if (gameObject.Name.TextValue.Equals(chibiTarget.Value)) flag = true;
                                break;
                            case ChibiType.DataId:
                                if (gameObject.DataId.ToString() == chibiTarget.Value) flag = true;
                                break;
                            case ChibiType.ObjectId:
                                if (gameObject.ObjectId.ToString() == chibiTarget.Value) flag = true;
                                break;
                            case ChibiType.ModelCharaId:
                                Character bc = (Character)gameObject;
                                CharacterStruct* bcs = (CharacterStruct*)bc.Address;
                                CharacterData cd = bcs->CharacterData;
                                if (cd.ModelCharaId.ToString() == chibiTarget.Value) flag = true;
                                cd.ModelScale = chibiTarget.Scale;
                                break;
                            default:
                                break;
                        }
                        if (flag)
                        {
                            var gos = (GameObjectStruct*)gameObject.Address;
                            gos->Scale = chibiTarget.Scale;
                        }
                    }
                    catch (Exception e)
                    {
                        continue;
                    }
                }
            }
        }
    }


    public override void Uninit()
    {
        Service.PluginInterface.UiBuilder.Draw -= Draw;
    }
}
