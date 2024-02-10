using ECommons.Automation;
using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DailyRoutines.Managers;

public class NotificationManager
{
    [DllImport("winmm.dll", SetLastError = true)]
    private static extern bool PlaySound(string pszSound, IntPtr hMod, uint fdwSound);

    private static NotifyIcon? Icon;
    private static TaskManager? TaskManager;

    private const uint SND_ASYNC = 0x0001;
    public const uint SND_ALIAS = 0x00010000;

    internal void Init()
    {
        TaskManager ??= new TaskManager { AbortOnTimeout = false, TimeLimitMS = int.MaxValue, ShowDebug = false };
    }

    public void ShowWindowsToast(string title, string content, ToolTipIcon icon = ToolTipIcon.Info)
    {
        if (Icon is not { Visible: true }) CreateIcon();

        TaskManager.Enqueue(() => ShowBalloonTip(title, content, icon));
        TaskManager.DelayNext(2000);
        TaskManager.Enqueue(DestroyIcon);
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
        if (TaskManager.NumQueuedTasks > 1) return;
        if (Icon != null)
        {
            Icon.Visible = false;
            Icon.Dispose();
            Icon = null;
        }
    }

    internal void Dispose()
    {
        TaskManager?.Abort();

        if (Icon != null) Icon.Visible = false;
        Icon?.Dispose();
        Icon = null;
    }
}
