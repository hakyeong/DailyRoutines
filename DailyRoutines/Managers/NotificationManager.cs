using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Timers;
using System.Windows.Forms;
using Timer = System.Timers.Timer;

namespace DailyRoutines.Managers;

public class NotificationManager
{
    private class BalloonTipMessage(DateTime time, Action messageAction)
    {
        public DateTime? Time { get; set; } = time;
        public Action MessageAction { get; set; } = messageAction;
    }

    private static NotifyIcon? Icon;

    private readonly Queue<BalloonTipMessage> messagesQueue = new();
    private readonly Timer timer = new(500);
    private readonly Stopwatch stopwatch = new();

    internal void Init()
    {
        timer.AutoReset = false;
        timer.Elapsed += OnTimerElapsed;
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (stopwatch.IsRunning && stopwatch.Elapsed < TimeSpan.FromMilliseconds(5)) return;

        if (Icon is not { Visible: true }) CreateIcon();
        switch (messagesQueue.Count)
        {
            case > 1:
                ShowBalloonTip(
                    "", Service.Lang.GetText("NotificationManager-ReceiveMultipleMessages", messagesQueue.Count));
                messagesQueue.Clear();

                stopwatch.Restart();
                timer.Restart();
                break;
            case 1:
                messagesQueue.Dequeue().MessageAction.Invoke();

                stopwatch.Reset();
                timer.Restart();
                break;
            default:
                DestroyIcon();
                break;
        }
    }

    public void Show(string title, string content, ToolTipIcon icon = ToolTipIcon.Info)
    {
        messagesQueue.Enqueue(new BalloonTipMessage(DateTime.Now, () => ShowBalloonTip(title, content, icon)));

        if (!stopwatch.IsRunning)
        {
            stopwatch.Start();
            timer.Start();
        }
    }

    private void ShowBalloonTip(string title, string content, ToolTipIcon icon = ToolTipIcon.Info)
    {
        Icon.ShowBalloonTip(500, string.IsNullOrEmpty(title) ? P.Name : SanitizeManager.Sanitize(title),
                            SanitizeManager.Sanitize(content), icon);
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
        stopwatch.Stop();

        timer.Elapsed -= OnTimerElapsed;
        timer.Stop();
        timer.Dispose();

        if (Icon != null) Icon.Visible = false;
        Icon?.Dispose();
        Icon = null;
    }
}
