using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ClickLib;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.AddonLifecycle;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text.SeStringHandling;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoCutSceneSkipTitle", "AutoCutSceneSkipDescription", ModuleCategories.Base)]
public class AutoCutSceneSkip : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithUI => false;

    private static TaskManager? TaskManager;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, int wParam, int lParam);

    private const uint Wm_Keydown = 0x0100;
    private const uint Wm_Keyup = 0x0101;
    private const int Vk_Esc = 0x1B;

    public void Init()
    {
        Service.Condition.ConditionChange += OnConditionChanged;
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = false };
    }

    private static void OnConditionChanged(ConditionFlag flag, bool value)
    {
        if (flag is ConditionFlag.OccupiedInCutSceneEvent or ConditionFlag.WatchingCutscene78)
        {
            if (value)
            {
                Task.Delay(1000)
                    .ContinueWith(_ => Service.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, "NowLoading", OnAddonLoading));
                Service.Toast.ErrorToast += OnErrorToast;
            }
            else
            {
                AbortActions();
            }
        }
    }

    private static void OnErrorToast(ref SeString message, ref bool isHandled)
    {
        if (message.ExtractText().Contains("该过场剧情无法跳过"))
        {
            AbortActions();
            message = SeString.Empty; 
            isHandled = true;
        }
    }

    private static void OnAddonLoading(AddonEvent type, AddonArgs args)
    {
        PressEsc();
        ClickExit();
    }

    private static void PressEsc()
    {
        var windowHandle = Process.GetCurrentProcess().MainWindowHandle;
        PostMessage(windowHandle, Wm_Keydown, Vk_Esc, 0);
        Task.Delay(50).ContinueWith(_ => PostMessage(windowHandle, Wm_Keyup, Vk_Esc, 0));
    }

    private static unsafe void ClickExit()
    {
        if (TryGetAddonByName<AtkUnitBase>("SystemMenu", out var menu) && IsAddonReady(menu))
        {
            Callback.Fire(menu, true, -1);
            AbortActions();
            return;
        }

        if (TryGetAddonByName<AtkUnitBase>("SelectString", out var addon) && IsAddonReady(addon))
        {
            if (addon->GetTextNodeById(2)->NodeText.ExtractText().Contains("要跳过这段过场动画吗"))
            {
                if (Click.TrySendClick("select_string1")) AbortActions();
            }
        }
    }

    private static void AbortActions()
    {
        TaskManager?.Abort();
        Service.AddonLifecycle.UnregisterListener(OnAddonLoading);
        Service.Toast.ErrorToast -= OnErrorToast;
    }

    public void UI() { }

    public void Uninit()
    {
        Service.Condition.ConditionChange -= OnConditionChanged;
        AbortActions();
    }
}
