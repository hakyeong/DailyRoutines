using System;
using System.Runtime.InteropServices;
using System.Timers;
using ClickLib;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Interface.Colors;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;
using TaskManager = ECommons.Automation.TaskManager;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoNoviceNetworkTitle", "AutoNoviceNetworkDescription", ModuleCategories.General)]
public class AutoNoviceNetwork : DailyModuleBase
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint Size;
        public uint LastInputTickCount;
    }

    [DllImport("User32.dll")]
    private static extern bool GetLastInputInfo(ref LastInputInfo info);

    private static Timer? AfkTimer;
    private static int TryTimes;
    private static bool IsInNoviceNetwork;
    private static bool ConfigIsTryJoinWhenInactive;

    public override void Init()
    {
        AddConfig("IsTryJoinWhenInactive", false);
        ConfigIsTryJoinWhenInactive = GetConfig<bool>("IsTryJoinWhenInactive");

        AfkTimer ??= new Timer(10000);
        AfkTimer.Elapsed += OnAfkStateCheck;
        AfkTimer.AutoReset = true;
        AfkTimer.Enabled = true;

        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = false };
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "SelectYesno", ClickYesButton);
    }

    public override void ConfigUI()
    {
        ImGui.BeginDisabled(TaskManager.IsBusy);
        if (ImGui.Button(Service.Lang.GetText("Start")))
        {
            TryTimes = 0;
            TaskManager.Enqueue(EnqueueARound);
        }
        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("Stop")))
            TaskManager.Abort();

        ImGui.SameLine();
        ImGui.TextWrapped($"{Service.Lang.GetText("AutoNoviceNetwork-AttemptedTimes")}:");

        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudYellow);
        ImGui.TextWrapped(TryTimes.ToString());
        ImGui.PopStyleColor();

        if (ImGui.Checkbox(Service.Lang.GetText("AutoNoviceNetwork-TryJoinWhenInactive"), ref ConfigIsTryJoinWhenInactive))
        {
            UpdateConfig("IsTryJoinWhenInactive", ConfigIsTryJoinWhenInactive);
        }

        ImGuiOm.HelpMarker(Service.Lang.GetText("AutoNoviceNetwork-TryJoinWhenInactiveHelp"));

        ImGui.SameLine();
        ImGui.Text($"{Service.Lang.GetText("AutoNoviceNetwork-JoinState")}:");

        ImGui.SameLine();
        ImGui.TextColored(IsInNoviceNetwork ? ImGuiColors.HealerGreen : ImGuiColors.DPSRed,
                          IsInNoviceNetwork ? Service.Lang.GetText("AutoNoviceNetwork-HaveJoined") : Service.Lang.GetText("AutoNoviceNetwork-HaveNotJoined"));

        ImGui.SameLine();
        if (ImGui.SmallButton(Service.Lang.GetText("Refresh")))
        {
            IsInNoviceNetwork = false;
        }
    }

    private unsafe void ClickYesButton(AddonEvent type, AddonArgs args)
    {
        if (!TaskManager.IsBusy) return;
        if (TryGetAddonByName<AddonSelectYesno>("SelectYesno", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(&addon->AtkUnitBase))
        {
            if (addon->PromptText->NodeText.ExtractText().Contains("新人频道"))
                Click.SendClick("select_yes");
        }
    }

    private void EnqueueARound()
    {
        TaskManager.Enqueue(ClickNoviceNetworkButton);
        TaskManager.DelayNext(500);
        TaskManager.Enqueue(() => CheckJoinState(false));
        TryTimes++;
    }

    private static unsafe void ClickNoviceNetworkButton()
    {
        AgentManager.SendEvent(AgentId.ChatLog, 0, 3);
    }

    private void CheckJoinState(bool isOnlyOneRound)
    {
        if (Service.Gui.GetAddonByName("BeginnerChatList") != nint.Zero)
        {
            IsInNoviceNetwork = true;
            TaskManager.Abort();
        }
        else if (!isOnlyOneRound)
            EnqueueARound();
    }

    private unsafe void OnAfkStateCheck(object? sender, ElapsedEventArgs e)
    {
        if (!ConfigIsTryJoinWhenInactive || IsInNoviceNetwork || TaskManager.IsBusy) return;
        if (Flags.BoundByDuty() || Flags.OccupiedInEvent()) return;

        var idleTime = GetIdleTime();
        if (idleTime > TimeSpan.FromSeconds(10) || Framework.Instance()->WindowInactive)
        {
            TaskManager.Enqueue(ClickNoviceNetworkButton);
            TaskManager.DelayNext(500);
            TaskManager.Enqueue(() => CheckJoinState(true));
        }
    }

    public static TimeSpan GetIdleTime()
    {
        var lastInputInfo = new LastInputInfo { Size = (uint)Marshal.SizeOf(typeof(LastInputInfo)) };
        GetLastInputInfo(ref lastInputInfo);

        return TimeSpan.FromMilliseconds(Environment.TickCount - (int)lastInputInfo.LastInputTickCount);
    }

    public override void Uninit()
    {
        AfkTimer?.Stop();
        if (AfkTimer != null) AfkTimer.Elapsed -= OnAfkStateCheck;
        AfkTimer?.Dispose();
        AfkTimer = null;

        IsInNoviceNetwork = false;
        Service.AddonLifecycle.UnregisterListener(ClickYesButton);
        TryTimes = 0;

        base.Uninit();
    }
}
