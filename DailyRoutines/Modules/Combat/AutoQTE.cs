using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.AddonLifecycle;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoQTETitle", "AutoQTEDescription", ModuleCategories.Combat)]
public class AutoQTE : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithConfigUI => false;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, int wParam, int lParam);

    private static readonly string[] QTETypes = ["_QTEKeep", "_QTEMash", "_QTEKeepTime", "_QTEButton"];

    private static nint WindowHandle = nint.Zero;

    private const uint WmKeydown = 0x0100;
    private const uint WmKeyup = 0x0101;
    private const int VkSpace = 0x20;
    private const int VkW = 0x57;

    public void Init()
    {
        WindowHandle = Process.GetCurrentProcess().MainWindowHandle;
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, QTETypes, OnQTEAddon);
    }

    public void ConfigUI() { }

    private static void OnQTEAddon(AddonEvent type, AddonArgs args)
    {
        if (WindowHandle == nint.Zero) return;
        PostMessage(WindowHandle, WmKeydown, VkSpace, 0);
        Task.Delay(50).ContinueWith(_ => PostMessage(WindowHandle, WmKeyup, VkSpace, 0));

        PostMessage(WindowHandle, WmKeydown, VkW, 0);
        Task.Delay(50).ContinueWith(_ => PostMessage(WindowHandle, WmKeyup, VkW, 0));
    }

    public void OverlayUI() { }

    public void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnQTEAddon);

        Initialized = false;
    }
}
