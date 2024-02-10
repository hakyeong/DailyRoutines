using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Timer = System.Timers.Timer;

namespace DailyRoutines.Managers;

public class NotificationManager
{
    private class BalloonTipMessage(string? title, string content, ToolTipIcon icon = ToolTipIcon.Info)
    {
        public string? Title { get; set; } = title;
        public string Content { get; set; } = content;
        public ToolTipIcon Icon { get; set; } = icon;
    }

    [DllImport("winmm.dll", SetLastError = true)]
    private static extern bool PlaySound(string pszSound, IntPtr hMod, uint fdwSound);

    private static NotifyIcon? Icon;

    private const uint SND_ASYNC = 0x0001;
    public const uint SND_ALIAS = 0x00010000;

    private readonly Queue<Action> messagesQueue = new();
    private readonly Timer timer = new(500);

    internal void Init()
    {
        timer.AutoReset = false;
        timer.Elapsed += OnTimerElapsed;
    }

    private void OnTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        switch (messagesQueue.Count)
        {
            case > 1:
                ShowBalloonTip("", Service.Lang.GetText("NotificationManager-ReceiveMultipleMessages", messagesQueue.Count));
                messagesQueue.Clear();
                timer.Restart();
                break;
            case 1:
                messagesQueue.Dequeue().Invoke();
                timer.Restart();
                break;
            default:
                DestroyIcon();
                break;
        }
    }

    public void Show(string title, string content, ToolTipIcon icon = ToolTipIcon.Info)
    {
        if (Icon is not { Visible: true }) CreateIcon();

        messagesQueue.Enqueue(() => ShowBalloonTip(title, content, icon));
        timer.Restart();
    }

    private void ShowBalloonTip(string title, string content, ToolTipIcon icon = ToolTipIcon.Info)
    {
        PlaySound("SystemAsterisk", IntPtr.Zero, SND_ASYNC | SND_ALIAS);
        Icon.ShowBalloonTip(500, string.IsNullOrEmpty(title) ? P.Name : SanitizeManager.Sanitize(title), SanitizeManager.Sanitize(content), icon);
    }

    private void CreateIcon()
    {
        DestroyIcon();
        Icon = new NotifyIcon
        {
            Icon = new Icon(Path.Join(P.PluginInterface.AssemblyLocation.DirectoryName, "Assets", "FFXIVICON.ico")),
            Text = P.Name,
            Visible = true
        };
    }

    private void DestroyIcon()
    {
        if (Icon != null)
        {
            Icon.Visible = false;
            Icon.Dispose();
            Icon = null;
        }
    }

    internal void Dispose()
    {
        timer.Elapsed -= OnTimerElapsed;
        timer.Stop();
        timer.Dispose();

        if (Icon != null) Icon.Visible = false;
        Icon?.Dispose();
        Icon = null;
    }
}
