using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DailyRoutines.Managers;

public class NotificationManager
{
    [DllImport("winmm.dll", SetLastError = true)]
    private static extern bool PlaySound(string pszSound, IntPtr hMod, uint fdwSound);

    private static NotifyIcon? Icon;

    private const uint SND_ASYNC = 0x0001;
    public const uint SND_ALIAS = 0x00010000;

    public void ShowWindowsToast(string title, string content, ToolTipIcon icon = ToolTipIcon.Info)
    {
        if (Icon is not { Visible: true }) CreateIcon();

        Task.Delay(2000).ContinueWith(_ => DestroyIcon());

        PlaySound("SystemAsterisk", IntPtr.Zero, SND_ASYNC | SND_ALIAS);
        Icon.ShowBalloonTip(500, string.IsNullOrEmpty(title) ? P.Name : title, content, icon);
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
        if (Icon != null) Icon.Visible = false;
        Icon?.Dispose();
        Icon = null;
    }
}
