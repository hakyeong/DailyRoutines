using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.AddonLifecycle;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoQTETitle", "AutoQTEDescription", ModuleCategories.Duty)]
public class AutoQTE : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithUI => false;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, int wParam, int lParam);

    private static readonly string[] QTETypes = ["_QTEKeep", "_QTEMash", "_QTEKeepTime", "_QTEButton"];

    private const uint WmKeydown = 0x0100;
    private const uint WmKeyup = 0x0101;
    private const int VkSpace = 0x20;
    private const int VkW = 0x57;

    public void Init()
    {
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, QTETypes, OnQTEAddon);
    }

    public void UI() { }

    private static void OnQTEAddon(AddonEvent type, AddonArgs args)
    {
        var windowHandle = Process.GetCurrentProcess().MainWindowHandle;
        PostMessage(windowHandle, WmKeydown, VkSpace, 0);
        Task.Delay(50).ContinueWith(_ => PostMessage(windowHandle, WmKeyup, VkSpace, 0));

        PostMessage(windowHandle, WmKeydown, VkW, 0);
        Task.Delay(50).ContinueWith(_ => PostMessage(windowHandle, WmKeyup, VkW, 0));
    }

    public void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnQTEAddon);

        Initialized = false;
    }
}
