using System;
using System.Windows.Forms;

using static PInvoke.User32;

namespace DailyRoutines.Helpers;

public class WindowHelper
{
    public static bool TryFindGameWindow(out nint hwnd)
    {
        hwnd = nint.Zero;
        while (true)
        {
            hwnd = FindWindowEx(nint.Zero, hwnd, "FFXIVGAME", null);
            if (hwnd == nint.Zero) break;
            _ = GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == Environment.ProcessId) break;
        }

        return hwnd != nint.Zero;
    }

    public static bool ApplicationIsActivated()
    {
        var activatedHandle = GetForegroundWindow();
        if (activatedHandle == nint.Zero)
            return false;

        var procId = Environment.ProcessId;
        _ = GetWindowThreadProcessId(activatedHandle, out var activeProcId);

        return activeProcId == procId;
    }

    public static bool SendKeypress(Keys key) => SendKeypress((int)key);

    public static bool SendMousepress(Keys key) => SendKeypress((int)key);

    public static bool SendKeypress(int key)
    {
        if (TryFindGameWindow(out var h))
        {
            NotifyHelper.Verbose($"Sending key {key}");
            SendMessage(h, WindowMessage.WM_KEYDOWN, key, 0);
            SendMessage(h, WindowMessage.WM_KEYUP, key, 0);
            return true;
        }

        NotifyHelper.Error("Couldn't find game window!");
        return false;
    }

    public static void SendMousepress(int key)
    {
        if (!TryFindGameWindow(out var h))
        {
            NotifyHelper.Error("Couldn't find game window!");
            return;
        }

        switch (key)
        {
            // XButton1
            case 1 | 4:
            {
                var wparam = MAKEWPARAM(0, 0x0001);
                SendMessage(h, WindowMessage.WM_XBUTTONDOWN, wparam, 0);
                SendMessage(h, WindowMessage.WM_XBUTTONUP, wparam, 0);
                break;
            }
            // XButton2
            case 2 | 4:
            {
                var wparam = MAKEWPARAM(0, 0x0002);
                SendMessage(h, WindowMessage.WM_XBUTTONDOWN, wparam, 0);
                SendMessage(h, WindowMessage.WM_XBUTTONUP, wparam, 0);
                break;
            }
            default:
                NotifyHelper.Error($"Invalid key: {key}");
                break;
        }
    }

    public static int MAKEWPARAM(int l, int h) => (l & 0xFFFF) | (h << 16);
}
