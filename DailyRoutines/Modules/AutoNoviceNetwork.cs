using System.Threading.Tasks;
using ClickLib;
using ClickLib.Bases;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.AddonLifecycle;
using Dalamud.Interface.Colors;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoNoviceNetworkTitle", "AutoNoviceNetworkDescription", ModuleCategories.General)]
public class AutoNoviceNetwork : IDailyModule
{
    public bool Initialized { get; set; }

    private static bool IsOnProcessing;
    private static int TryTimes;

    public void Init()
    {
        Initialized = true;
    }

    public void UI()
    {
        ImGui.BeginDisabled(IsOnProcessing);
        if (ImGui.Button("开始"))
        {
            TryTimes = 0;
            Service.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, "SelectYesno", ClickYesButton);
            IsOnProcessing = true;

            ClickNoviceNetworkButton();
        }

        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button("结束")) EndProcess();

        ImGui.SameLine();
        ImGui.TextWrapped("已尝试次数:");

        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudYellow);
        ImGui.TextWrapped(TryTimes.ToString());
        ImGui.PopStyleColor();
    }

    private static void ClickYesButton(AddonEvent type, AddonArgs args)
    {
        if (EzThrottler.Throttle("AutoNoviceNetwork-ClickYesButton", 100)) Click.SendClick("select_yes");
    }

    private static unsafe void ClickNoviceNetworkButton()
    {
        if (TryGetAddonByName<AtkUnitBase>("ChatLog", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(addon))
        {
            var buttonNode = addon->GetComponentNodeById(12);
            if (buttonNode != null)
            {
                var handler = new ClickChatLog();
                handler.NoviceNetworkButton();
                TryTimes++;

                Task.Delay(500).ContinueWith(t => CheckJoinState());
            }
            else
                EndProcess();
        }
        else
            EndProcess();
    }

    private static unsafe void CheckJoinState()
    {
        if (TryGetAddonByName<AtkUnitBase>("BeginnerChatList", out _))
            EndProcess();
        else
            ClickNoviceNetworkButton();
    }

    private static void EndProcess()
    {
        Service.AddonLifecycle.UnregisterListener(ClickYesButton);
        IsOnProcessing = false;
    }

    public void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(ClickYesButton);
        IsOnProcessing = false;
        TryTimes = 0;

        Initialized = false;
    }
}

public class ClickChatLog(nint addon = default) : ClickBase<ClickChatLog>("ChatLog", addon)
{
    public bool NoviceNetworkButton()
    {
        FireCallback(3);
        return true;
    }
}
