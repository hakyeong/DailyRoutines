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
public class AutoAntiAfk : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithConfigUI => false;

    private static Timer? AfkTimer;

    public void Init()
    {
        AfkTimer ??= new Timer(10000);
        AfkTimer.Elapsed += OnAfkStateCheck;
        AfkTimer.AutoReset = true;
        AfkTimer.Enabled = true;
    }

    public void ConfigUI() { }

    public void OverlayUI() { }

    private static unsafe void OnAfkStateCheck(object? sender, ElapsedEventArgs e)
    {
        if (Framework.Instance()->WindowInactive)
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

    public void Uninit()
    {
        AfkTimer?.Stop();
        if (AfkTimer != null) AfkTimer.Elapsed -= OnAfkStateCheck;
        AfkTimer?.Dispose();
        AfkTimer = null;
    }
}
