using System;
using System.Runtime.InteropServices;
using System.Timers;
using DailyRoutines.Helpers;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Interface.Colors;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoNoviceNetworkTitle", "AutoNoviceNetworkDescription", ModuleCategories.一般)]
public unsafe class AutoNoviceNetwork : DailyModuleBase
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint Size;
        public uint LastInputTickCount;
    }

    [StructLayout(LayoutKind.Explicit, Size = 40)]
    private struct InfoProxyBeginner
    {
        [FieldOffset(0)]
        public InfoProxyInterface InfoProxyInterface;

        [FieldOffset(24)]
        public bool IsInNoviceNetwork;

        public static InfoProxyBeginner* Instance() => (InfoProxyBeginner*)InfoModule.Instance()->GetInfoProxyById((InfoProxyId)20);
    }

    private delegate byte TryJoinNoviceNetworkDelegate(InfoProxyInterface* infoProxy20);
    [Signature("E8 ?? ?? ?? ?? 45 33 F6 41 B4")]
    private static TryJoinNoviceNetworkDelegate? TryJoinNoviceNetwork;

    private delegate bool IsNoviceNetworkFlagSetDelegate(PlayerState* instance, uint flag);
    // 传入 8U 检测是否启用自动加入新人频道
    [Signature("8B C2 44 8B C2 C1 E8 ?? 4C 8B C9 83 F8 ?? 72 ?? 32 C0 C3 41 83 E0 ?? BA ?? ?? ?? ?? 41 0F B6 C8 D2 E2", ScanType = ScanType.Text)]
    private static IsNoviceNetworkFlagSetDelegate? IsNoviceNetworkFlagSet;

    private static Timer? AfkTimer;
    private static int TryTimes;
    private static bool IsTryJoinWhenInactive;
    private static bool IsInNoviceNetworkDisplay;

    [DllImport("User32.dll")]
    private static extern bool GetLastInputInfo(ref LastInputInfo info);

    public override void Init()
    {
        Service.Hook.InitializeFromAttributes(this);

        AddConfig("IsTryJoinWhenInactive", false);
        IsTryJoinWhenInactive = GetConfig<bool>("IsTryJoinWhenInactive");

        AfkTimer ??= new Timer(10000);
        AfkTimer.Elapsed += OnAfkStateCheck;
        AfkTimer.AutoReset = true;
        AfkTimer.Enabled = true;

        TaskHelper ??= new TaskHelper { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = false };
    }

    public override void ConfigUI()
    {
        ImGui.BeginDisabled(TaskHelper.IsBusy);
        if (ImGui.Button(Service.Lang.GetText("Start")))
        {
            Service.Hook.InitializeFromAttributes(this);

            TryTimes = 0;
            TaskHelper.Enqueue(EnqueueARound);
        }
        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("Stop")))
            TaskHelper.Abort();

        ImGui.SameLine();
        ImGui.TextWrapped($"{Service.Lang.GetText("AutoNoviceNetwork-AttemptedTimes")}:");

        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudYellow);
        ImGui.TextWrapped(TryTimes.ToString());
        ImGui.PopStyleColor();

        if (ImGui.Checkbox(Service.Lang.GetText("AutoNoviceNetwork-TryJoinWhenInactive"),
                           ref IsTryJoinWhenInactive))
            UpdateConfig("IsTryJoinWhenInactive", IsTryJoinWhenInactive);

        ImGuiOm.HelpMarker(Service.Lang.GetText("AutoNoviceNetwork-TryJoinWhenInactiveHelp"));

        ImGui.SameLine();
        ImGui.Text($"{Service.Lang.GetText("AutoNoviceNetwork-JoinState")}:");

        if (Throttler.Throttle("AutoNoviceNetwork", 1000))
            IsInNoviceNetworkDisplay = IsInNoviceNetwork;

        ImGui.SameLine();
        ImGui.TextColored(IsInNoviceNetworkDisplay ? ImGuiColors.HealerGreen : ImGuiColors.DPSRed,
                          IsInNoviceNetworkDisplay
                              ? Service.Lang.GetText("AutoNoviceNetwork-HaveJoined")
                              : Service.Lang.GetText("AutoNoviceNetwork-HaveNotJoined"));
    }

    private void EnqueueARound()
    {
        TaskHelper.Enqueue(() =>
        {
            if (!IsNoviceNetworkFlagSet(PlayerState.Instance(), 8U))
                ChatHelper.Instance.SendMessage("/beginnerchannel on");
        });

        TaskHelper.Enqueue(TryJoin);

        TaskHelper.DelayNext(250);
        TaskHelper.Enqueue(() => TryTimes++);

        TaskHelper.Enqueue(() =>
        {
            if (IsInNoviceNetwork)
            {
                TaskHelper.Abort();
                return;
            }

            EnqueueARound();
        });
    }

    private static void TryJoin()
        => TryJoinNoviceNetwork(&InfoProxyBeginner.Instance()->InfoProxyInterface);

    private static bool IsInNoviceNetwork => InfoProxyBeginner.Instance()->IsInNoviceNetwork;

    private void OnAfkStateCheck(object? sender, ElapsedEventArgs e)
    {
        if (!IsTryJoinWhenInactive || IsInNoviceNetwork || TaskHelper.IsBusy) return;
        if (Flags.BoundByDuty || Flags.OccupiedInEvent) return;

        var idleTime = GetIdleTime();
        if (idleTime > TimeSpan.FromSeconds(10) || Framework.Instance()->WindowInactive)
            TryJoin();
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

        TryTimes = 0;
        base.Uninit();
    }
}
