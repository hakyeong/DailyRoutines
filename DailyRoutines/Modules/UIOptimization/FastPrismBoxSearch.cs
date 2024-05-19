using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ClickLib.Bases;
using DailyRoutines.Helpers;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using DailyRoutines.Windows;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;

namespace DailyRoutines.Modules;

[ModuleDescription("FastPrismBoxSearchTitle", "FastPrismBoxSearchDescription", ModuleCategories.界面优化)]
public unsafe class FastPrismBoxSearch : DailyModuleBase
{
    private sealed class ClickCabinetWithdraw(nint addon = default)
        : ClickBase<ClickCabinetWithdraw, AddonTalk>("CabinetWithdraw", addon)
    {
        public static implicit operator ClickCabinetWithdraw(nint addon) => new(addon);

        public static ClickCabinetWithdraw Using(nint addon) => new(addon);

        public void Click(AtkComponentRadioButton* button) => ClickAddonRadioButton(button, 11);
    }

    private enum Sex : uint
    {
        不设限,
        符合当前性别,
        男性,
        女性
    }

    private enum PlateSlot : uint
    {
        主手,
        副手,
        头部,
        身体,
        手臂,
        腿部,
        脚部,
        耳部,
        颈部,
        腕部,
        右指,
        左指
    }

    private class Config : ModuleConfiguration
    {
        public PlateSlot DefaultSlot = PlateSlot.身体;
    }

    private static AtkUnitBase* MiragePrismPrismBox => (AtkUnitBase*)Service.Gui.GetAddonByName("MiragePrismPrismBox");
    private static AtkUnitBase* MiragePrismMiragePlate => 
        (AtkUnitBase*)Service.Gui.GetAddonByName("MiragePrismMiragePlate");
    private static AtkUnitBase* CabinetWithdraw => (AtkUnitBase*)Service.Gui.GetAddonByName("CabinetWithdraw");

    private static Config ModuleConfig = null!;

    private static Dictionary<uint, string> AllJobs = [];

    private static int ClassJobInput = 0;
    private static int SexInput;
    private static string SearchInput = string.Empty;

    private static Vector2 WindowSize;

    public override void Init()
    {
        ModuleConfig = LoadConfig<Config>() ?? new();

        foreach (var classJob in LuminaCache.Get<ClassJob>())
        {
            if (classJob.RowId < 8 || string.IsNullOrWhiteSpace(classJob.Name.RawString)) continue;
            if (classJob.ClassJobParent.Row != classJob.RowId)
                AllJobs.Remove(classJob.ClassJobParent.Row);
            AllJobs.TryAdd(classJob.RowId, classJob.Name.RawString);
        }

        AllJobs.TryAdd(0, "全部显示");
        AllJobs = AllJobs.Reverse().ToDictionary(x => x.Key, x => x.Value);
        Overlay ??= new Overlay(this);

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "MiragePrismMiragePlate", OnAddonPlate);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "MiragePrismMiragePlate", OnAddonPlate);
    }

    public override void ConfigUI()
    {
        ImGui.AlignTextToFramePadding();
        ImGui.Text($"{Service.Lang.GetText("FastPrismBoxSearch-DefaultSlot")}:");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(ImGui.CalcTextSize("主手副手").X + 2 * ImGui.GetStyle().ItemSpacing.X);
        if (ImGui.BeginCombo("###DefaultSlotCombo", ModuleConfig.DefaultSlot.ToString(), ImGuiComboFlags.HeightLarge))
        {
            foreach (var plateSlot in Enum.GetValues<PlateSlot>())
            {
                if (ImGui.RadioButton(plateSlot.ToString(), plateSlot == ModuleConfig.DefaultSlot))
                {
                    ModuleConfig.DefaultSlot = plateSlot;
                    SaveConfig(ModuleConfig);
                }
            }

            ImGui.EndCombo();
        }
    }

    public override void OverlayPreDraw()
    {
        if (MiragePrismPrismBox == null)
            Overlay.IsOpen = false;
    }

    public override void OverlayUI()
    {
        var addon = MiragePrismPrismBox;
        var pos = new Vector2(addon->GetX() + addon->GetScaledWidth(true) - 16f, addon->GetY() + 6);
        ImGui.SetWindowPos(pos);

        ImGui.AlignTextToFramePadding();
        ImGui.Text($"{Service.Lang.GetText("Sex")}:");

        foreach (var sex in Enum.GetValues<Sex>())
        {
            ImGui.SameLine();
            ImGui.RadioButton(sex.ToString(), ref SexInput, (int)sex);
        }

        ImGui.AlignTextToFramePadding();
        ImGui.Text($"{Service.Lang.GetText("Job")}:");

        ImGui.SameLine();
        if (ImGui.BeginCombo("###JobCombo", AllJobs[(uint)ClassJobInput], ImGuiComboFlags.HeightLarge))
        {
            foreach (var job in AllJobs)
            {
                if (ImGuiOm.SelectableImageWithText(ImageHelper.GetIcon(62100 + (job.Key == 0 ? 44 : job.Key)).ImGuiHandle,
                                                    ImGuiHelpers.ScaledVector2(20f), job.Value, 
                                                    ClassJobInput == (int)job.Key))
                {
                    ClassJobInput = (int)job.Key;
                }
            }
            ImGui.EndCombo();
        }

        ImGui.AlignTextToFramePadding();
        ImGui.Text($"{Service.Lang.GetText("Search")}:");

        ImGui.SameLine();
        if (ImGui.InputText("###SearchKeywordInput", ref SearchInput, 64, ImGuiInputTextFlags.EnterReturnsTrue))
            Search(90, (Sex)SexInput, SearchInput);

        if (ImGui.IsItemDeactivatedAfterEdit())
            Search(90, (Sex)SexInput, SearchInput);

        WindowSize = ImGui.GetWindowSize();
    }

    private static void Search(uint level, Sex sex, string keyword)
    {
        AddonHelper.Callback(MiragePrismPrismBox, true, 8U, (uint)ClassJobInput);
        AgentHelper.SendEvent(AgentId.MiragePrismPrismBox, 16, level, (uint)sex, keyword);
        AddonHelper.Callback(CabinetWithdraw, true, 8, keyword);
    }

    private void OnAddonPlate(AddonEvent type, AddonArgs args)
    {
        if (MiragePrismPrismBox == null) return;

        if (type == AddonEvent.PostSetup)
            AddonHelper.Callback(MiragePrismMiragePlate, true, 13, (uint)ModuleConfig.DefaultSlot);

        Overlay.IsOpen = type switch
        {
            AddonEvent.PostSetup => true,
            AddonEvent.PreFinalize => false,
            _ => Overlay.IsOpen
        };

        switch (type)
        {
            case AddonEvent.PostSetup:
                AgentModule.Instance()->GetAgentByInternalId(AgentId.CabinetWithdraw)->Show();
                Service.Framework.RunOnTick(() =>
                {
                    CabinetWithdraw->SetPosition(
                        (short)(MiragePrismPrismBox->X + MiragePrismPrismBox->GetScaledWidth(true) - 24f),
                        (short)(MiragePrismPrismBox->Y + WindowSize.Y));

                    ClickCabinetWithdraw.Using((nint)CabinetWithdraw).Click(CabinetWithdraw->GetNodeById(23)->GetAsAtkComponentRadioButton());

                    AddonHelper.SetComponentButtonChecked
                        (CabinetWithdraw->GetNodeById(23)->GetAsAtkComponentButton(), true);
                    AddonHelper.SetComponentButtonChecked
                        (CabinetWithdraw->GetNodeById(12)->GetAsAtkComponentButton(), false);

                }, TimeSpan.FromMilliseconds(200), 0, FrameworkManager.CancelSource.Token);
                break;
            case AddonEvent.PreFinalize:
                AgentModule.Instance()->GetAgentByInternalId(AgentId.CabinetWithdraw)->Hide();
                break;
        }
    }
}
