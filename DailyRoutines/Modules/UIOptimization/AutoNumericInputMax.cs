using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Runtime.InteropServices;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoNumericInputMaxTitle", "AutoNumericInputMaxDescription", ModuleCategories.界面优化)]
public unsafe class AutoNumericInputMax : DailyModuleBase
{
    private delegate nint InitFromComponentDataDelegate(AtkComponentNumericInput* component, AtkUldComponentDataNumericInput* data);
    [Signature("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC ?? 0F 10 02 48 8B D9 0F 11 81", DetourName = nameof(InitFromComponentDataDetour))]
    private readonly Hook<InitFromComponentDataDelegate>? InitFromComponentDataHook;

    private delegate nint UldUpdateDelegate(AtkComponentNumericInput* component);
    [Signature("40 53 48 83 EC ?? 0F B6 81 ?? ?? ?? ?? 48 8B D9 48 83 C1 ?? A8 ?? 74 ?? 48 83 79 ?? ?? 74 ?? A8 ?? 75 ?? 48 83 79 ?? ?? 75 ?? E8 ?? ?? ?? ?? EB ?? E8 ?? ?? ?? ?? 80 BB ?? ?? ?? ?? ?? 74 ?? 48 83 BB", DetourName = nameof(UldUpdateDetour))]
    private readonly Hook<UldUpdateDelegate>? UldUpdateHook;

    private static long _LastInterruptTime;

    public override void Init()
    {
        Service.Hook.InitializeFromAttributes(this);
        // InitFromComponentDataHook?.Enable();
        UldUpdateHook?.Enable();
    }

    public override void ConfigUI()
    {
        ConflictKeyText();

        ImGuiOm.HelpMarker(Service.Lang.GetText("AutoNumericInputMax-InterruptHelp"));
    }

    private nint InitFromComponentDataDetour(AtkComponentNumericInput* component, AtkUldComponentDataNumericInput* data)
    {
        if (data->Max < 9999)
        {
            data->Value = data->Max;
            component->SetValue(data->Max);
        }
        var result = InitFromComponentDataHook.Original(component, data);

        // 一些界面初始化后还会再刷新
        if (data->Max < 9999)
            Service.Framework.RunOnTick(() => component->SetValue(data->Max), TimeSpan.FromMilliseconds(100));
        return result;
    }

    private nint UldUpdateDetour(AtkComponentNumericInput* component)
    {
        var result = UldUpdateHook.Original(component);

        // 一些界面切换 Tab 后也会刷新输入状态
        if (Service.KeyState[Service.Config.ConflictKey])
            _LastInterruptTime = Environment.TickCount64;

        if (Environment.TickCount64 - _LastInterruptTime > 10000)
            if (EzThrottler.Throttle($"AutoNumericInputMax-UldUpdate_{(nint)component}", 250))
            {
                var value = Marshal.ReadInt32((nint)component + 504);
                var nodeFlags = component->AtkComponentInputBase.AtkComponentBase.OwnerNode->AtkResNode.NodeFlags;
                if (value == 1 && nodeFlags.HasFlag(NodeFlags.Enabled | NodeFlags.Visible) && component->Data.Max < 9999)
                {
                    component->SetValue(component->Data.Max);
                }
            }

        return result;
    }
}
