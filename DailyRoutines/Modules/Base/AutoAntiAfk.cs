using System;
using System.Runtime.InteropServices;
using System.Timers;
using System.Windows.Forms;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.ClientState.Keys;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Timer = System.Timers.Timer;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoAntiAfkTitle", "AutoAntiAfkDescription", ModuleCategories.Base)]
public class AutoAntiAfk : DailyModuleBase
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

    public override void Init()
    {
        AfkTimer ??= new Timer(10000);
        AfkTimer.Elapsed += OnAfkStateCheck;
        AfkTimer.AutoReset = true;
        AfkTimer.Enabled = true;
    }

    private static unsafe void OnAfkStateCheck(object? sender, ElapsedEventArgs e)
    {
        var idleTime = GetIdleTime();
        if (idleTime > TimeSpan.FromSeconds(10) || Framework.Instance()->WindowInactive)
        {
            switch (Service.Config.ConflictKey)
            {
                case VirtualKey.LCONTROL or VirtualKey.RCONTROL or VirtualKey.CONTROL:
                    WindowsKeypress.SendKeypress(Keys.LShiftKey);
                    break;
                default:
                    WindowsKeypress.SendKeypress(Keys.LControlKey);
                    break;
            }
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

        base.Uninit();
    }
}
