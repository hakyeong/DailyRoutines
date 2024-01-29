using System.Threading.Tasks;
using ClickLib;
using DailyRoutines.Clicks;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.AddonLifecycle;
using Dalamud.Interface.Colors;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoNoviceNetworkTitle", "AutoNoviceNetworkDescription", ModuleCategories.General)]
public class AutoNoviceNetwork : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithUI => true;

    private static bool IsOnProcessing;
    private static int TryTimes;

    public void Init() { }

    public void UI()
    {
        ImGui.BeginDisabled(IsOnProcessing);
        if (ImGui.Button(Service.Lang.GetText("AutoNoviceNetwork-Start")))
        {
            TryTimes = 0;
            Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "SelectYesno", ClickYesButton);
            IsOnProcessing = true;

            ClickNoviceNetworkButton();
        }

        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("AutoNoviceNetwork-Stop"))) EndProcess();

        ImGui.SameLine();
        ImGui.TextWrapped($"{Service.Lang.GetText("AutoNoviceNetwork-AttemptedTimes")}:");

        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudYellow);
        ImGui.TextWrapped(TryTimes.ToString());
        ImGui.PopStyleColor();
    }

    private static unsafe void ClickYesButton(AddonEvent type, AddonArgs args)
    {
        if (TryGetAddonByName<AddonSelectYesno>("SelectYesno", out var addon) && HelpersOm.IsAddonAndNodesReady(&addon->AtkUnitBase))
        {
            if (addon->PromptText->NodeText.ExtractText().Contains("新人频道"))
            {
                Click.SendClick("select_yes");
            }
        }
    }

    private static unsafe void ClickNoviceNetworkButton()
    {
        if (!IsOnProcessing) return;
        if (TryGetAddonByName<AtkUnitBase>("ChatLog", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(addon))
        {
            var buttonNode = addon->GetComponentNodeById(12);
            if (buttonNode != null)
            {
                var handler = new ClickChatLogDR();
                handler.NoviceNetwork();
                TryTimes++;

                Task.Delay(500).ContinueWith(_ => CheckJoinState());
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
    }
}
