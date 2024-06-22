using System;
using System.Runtime.InteropServices;
using DailyRoutines.Managers;
using Dalamud.Hooking;
using Dalamud.Interface.Utility;
using Dalamud.Utility.Signatures;
using ImGuiNET;

namespace DailyRoutines.Modules;

// 完全来自 Pandora's Box, NiGuangOwO 写的
[ModuleDescription("PFPageSizeCustomizeTitle", "PFPageSizeCustomizeDescription", ModuleCategories.界面优化)]
public class PFPageSizeCustomize : DailyModuleBase
{
    private static int ConfigDisplayAmount = 100;

    [Signature(
        "48 89 5C 24 ?? 55 56 57 48 ?? ?? ?? ?? ?? ?? ?? 48 ?? ?? ?? ?? ?? ?? 48 ?? ?? ?? ?? ?? ?? 48 ?? ?? 48 89 85 ?? ?? ?? ?? 48 ?? ?? 0F",
        DetourName = nameof(PartyFinderDisplayAmountDetour))]
    private readonly Hook<PartyFinderDisplayAmountDelegate>? PartyFinderDisplayAmountHook;

    public override void Init()
    {
        Service.Hook.InitializeFromAttributes(this);
        PartyFinderDisplayAmountHook?.Enable();

        AddConfig("DisplayAmount", 100);
        ConfigDisplayAmount = GetConfig<int>("DisplayAmount");
    }

    public override void ConfigUI()
    {
        ImGui.SetNextItemWidth(100f * GlobalFontScale);
        if (ImGui.InputInt(Service.Lang.GetText("PFPageSizeCustomize-DisplayAmount"), ref ConfigDisplayAmount, 10, 10,
                           ImGuiInputTextFlags.EnterReturnsTrue))
        {
            ConfigDisplayAmount = Math.Clamp(ConfigDisplayAmount, 1, 100);
            UpdateConfig("DisplayAmount", ConfigDisplayAmount);
        }
    }

    private char PartyFinderDisplayAmountDetour(long a1, int a2)
    {
        Marshal.WriteInt16((nint)a1 + 904, (short)ConfigDisplayAmount);
        return PartyFinderDisplayAmountHook.Original(a1, a2);
    }

    private delegate char PartyFinderDisplayAmountDelegate(long a1, int a2);
}
