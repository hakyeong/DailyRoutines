using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.AddonLifecycle;
using ECommons.Throttlers;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoQTETitle", "AutoQTEDescription", ModuleCategories.General)]
public partial class AutoQTE : IDailyModule
{
    public bool Initialized { get; set; }

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostMessage(IntPtr hWnd, uint Msg, int wParam, int lParam);

    private const uint WmKeydown = 0x0100;
    private const uint WmKeyup = 0x0101;
    private const int VkSpace = 0x20;

    public void Init()
    {
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, "_QTEKeep", OnQTEAddon);

        Initialized = true;
    }

    public void UI() { }

    private static void OnQTEAddon(AddonEvent type, AddonArgs args)
    {
        if (EzThrottler.Throttle("AutoQTE", 100))
        {
            var windowHandle = Process.GetCurrentProcess().MainWindowHandle;
            PostMessage(windowHandle, WmKeydown, VkSpace, 0);
            Task.Delay(50).ContinueWith(_ => PostMessage(windowHandle, WmKeyup, VkSpace, 0));
        }
    }

    public void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnQTEAddon);

        Initialized = false;
    }
}
