using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using DailyRoutines.Helpers;
using DailyRoutines.Managers;
using ECommons.Automation;

namespace DailyRoutines.Notifications;

public class WinToast : DailyNotificationBase
{
    private class ToastMessage(string? title, string message, ToolTipIcon icon = ToolTipIcon.Info)
    {
        public string? Title { get; set; } = title;
        public string Message { get; set; } = message;
        public ToolTipIcon Icon { get; set; } = icon;
    }

    private static TaskManager? TaskManager;

    private static NotifyIcon? icon;
    private static readonly Queue<ToastMessage> messagesQueue = new();

    public override void Init()
    {
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 10000, ShowDebug = false };
    }

    public static void Notify(string title, string content, ToolTipIcon toolTipIcon = ToolTipIcon.Info)
    {
        if (icon == null || icon.Visible == false) CreateIcon();
        messagesQueue.Enqueue(new ToastMessage(title, content, toolTipIcon));

        TaskManager.DelayNext(200);
        TaskManager.Enqueue(TryDequeueMessages);
    }

    private static void TryDequeueMessages()
    {
        switch (messagesQueue.Count)
        {
            case 0:
                DestroyIcon();
                break;
            case 1:
                ShowBalloonTip(messagesQueue.Dequeue());

                TaskManager.DelayNext(6000);
                TaskManager.Enqueue(TryDequeueMessages);
                break;
            case >= 2:
                ShowBalloonTip(new ToastMessage("", Service.Lang.GetText("NotificationManager-ReceiveMultipleMessages", messagesQueue.Count)));
                messagesQueue.Clear();

                TaskManager.DelayNext(6000);
                TaskManager.Enqueue(TryDequeueMessages);
                break;
        }
    }

    private static void ShowBalloonTip(ToastMessage message)
    {
        icon.ShowBalloonTip(
            5000, string.IsNullOrEmpty(message.Title) ? Plugin.Name : SanitizeHelper.Sanitize(message.Title),
            SanitizeHelper.Sanitize(message.Message), message.Icon);
    }

    private static void CreateIcon()
    {
        DestroyIcon();
        icon = new NotifyIcon
        {
            Icon = new Icon(Path.Join(Service.PluginInterface.AssemblyLocation.DirectoryName, "Assets", "FFXIVICON.ico")),
            Text = Plugin.Name,
            Visible = true
        };
    }

    private static void DestroyIcon()
    {
        if (icon != null)
        {
            icon.Visible = false;
            icon.Dispose();
            icon = null;
        }
    }

    public override void Uninit()
    {
        DestroyIcon();
        TaskManager?.Abort();
    }
}
