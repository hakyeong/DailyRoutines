using System.Windows.Forms;
using ClickLib;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Internal.Notifications;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoCutSceneSkipTitle", "AutoCutSceneSkipDescription", ModuleCategories.Base)]
public class AutoCutSceneSkip : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithConfigUI => true;

    private static TaskManager? TaskManager;

    public void Init()
    {
        TaskManager ??= new TaskManager { TimeLimitMS = int.MaxValue, ShowDebug = false };
        Service.Condition.ConditionChange += OnConditionChanged;
    }

    public void ConfigUI()
    {
        ImGui.Text($"{Service.Lang.GetText("ConflictKey")}: {Service.Config.ConflictKey}");
        ImGuiOm.HelpMarker(Service.Lang.GetText("AutoCutSceneSkip-InterruptNotice"));
    }

    public void OverlayUI() { }

    private static void OnConditionChanged(ConditionFlag flag, bool value)
    {
        if (flag is ConditionFlag.OccupiedInCutSceneEvent or ConditionFlag.WatchingCutscene78)
        {
            if (value)
            {
                if (Service.KeyState[Service.Config.ConflictKey])
                {
                    P.PluginInterface.UiBuilder.AddNotification(Service.Lang.GetText("ConflictKey-InterruptMessage"),
                                                                "Daily Routines", NotificationType.Success);
                    return;
                }

                TaskManager.Enqueue(IsWatchingCutscene);
                Service.Toast.ErrorToast += OnErrorToast;
            }
            else
                AbortActions();
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

    private static unsafe bool? IsWatchingCutscene()
    {
        WindowsKeypress.SendKeypress(Keys.Escape);

        if (TryGetAddonByName<AtkUnitBase>("SystemMenu", out var menu) && HelpersOm.IsAddonAndNodesReady(menu))
        {
            Callback.Fire(menu, true, -1);
            menu->Hide(true);
            AbortActions();
            return true;
        }

        if (TryGetAddonByName<AtkUnitBase>("SelectString", out var addon) && HelpersOm.IsAddonAndNodesReady(addon))
        {
            if (addon->GetTextNodeById(2)->NodeText.ExtractText().Contains("要跳过这段过场动画吗"))
            {
                if (Click.TrySendClick("select_string1"))
                {
                    AbortActions();
                    return true;
                }
            }
        }

        return false;
    }

    private static void AbortActions()
    {
        TaskManager?.Abort();
        Service.Toast.ErrorToast -= OnErrorToast;
    }

    public void Uninit()
    {
        Service.Condition.ConditionChange -= OnConditionChanged;
        AbortActions();
    }
}
