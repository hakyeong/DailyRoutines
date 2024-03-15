using System.Threading.Tasks;
using ClickLib;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Interface.Colors;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoNoviceNetworkTitle", "AutoNoviceNetworkDescription", ModuleCategories.General)]
public class AutoNoviceNetwork : DailyModuleBase
{
    private static bool IsOnProcessing;
    private static int TryTimes;

    public override void ConfigUI()
    {
        ImGui.BeginDisabled(IsOnProcessing);
        if (ImGui.Button(Service.Lang.GetText("Start")))
        {
            TryTimes = 0;
            Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "SelectYesno", ClickYesButton);
            IsOnProcessing = true;

            ClickNoviceNetworkButton();
        }

        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("Stop"))) EndProcess();

        ImGui.SameLine();
        ImGui.TextWrapped($"{Service.Lang.GetText("AutoNoviceNetwork-AttemptedTimes")}:");

        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudYellow);
        ImGui.TextWrapped(TryTimes.ToString());
        ImGui.PopStyleColor();
    }

    private static unsafe void ClickYesButton(AddonEvent type, AddonArgs args)
    {
        if (TryGetAddonByName<AddonSelectYesno>("SelectYesno", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(&addon->AtkUnitBase))
            if (addon->PromptText->NodeText.ExtractText().Contains("新人频道"))
                Click.SendClick("select_yes");
    }

    private static unsafe void ClickNoviceNetworkButton()
    {
        if (!IsOnProcessing) return;
        var agent = AgentModule.Instance()->GetAgentByInternalId(AgentId.ChatLog);
        if (agent == null)
        {
            EndProcess();
            return;
        }

        AgentManager.SendEvent(agent, 0, 3);
        TryTimes++;

        Task.Delay(500).ContinueWith(_ => CheckJoinState());
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

    public override void Uninit()
    {
        EndProcess();
        TryTimes = 0;

        base.Uninit();
    }
}
