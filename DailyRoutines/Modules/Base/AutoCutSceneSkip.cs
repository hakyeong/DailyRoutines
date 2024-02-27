using System.Windows.Forms;
using ClickLib;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text.SeStringHandling;
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
        Service.Toast.ErrorToast += OnErrorToast;
    }

    public void ConfigUI()
    {
        ImGui.Text($"{Service.Lang.GetText("ConflictKey")}: {Service.Config.ConflictKey}");
    }

    public void OverlayUI() { }

    private static void OnConditionChanged(ConditionFlag flag, bool value)
    {
        if (flag is ConditionFlag.OccupiedInCutSceneEvent or ConditionFlag.WatchingCutscene78)
        {
            if (!value)
                AbortActions();
            else
                TaskManager.Enqueue(IsWatchingCutscene);
        }
    }

    private static void OnErrorToast(ref SeString message, ref bool isHandled)
    {
        if (!TaskManager.IsBusy) return;
        if (message.ExtractText().Contains("该过场剧情无法跳过"))
        {
            AbortActions();
            message = SeString.Empty;
            isHandled = true;
        }
    }

    private static unsafe bool? IsWatchingCutscene()
    {
        if (TryGetAddonByName<AtkUnitBase>("SystemMenu", out var menu) && HelpersOm.IsAddonAndNodesReady(menu))
        {
            AddonManager.Callback(menu, true, -1);
            menu->Hide(true);

            TaskManager.Abort();
            return true;
        }

        WindowsKeypress.SendKeypress(Keys.Escape);

        if (TryGetAddonByName<AtkUnitBase>("SelectString", out var addon) && HelpersOm.IsAddonAndNodesReady(addon))
        {
            if (addon->GetTextNodeById(2)->NodeText.ExtractText().Contains("要跳过这段过场动画吗"))
            {
                Click.SendClick("select_string1");
            }
        }

        if (Service.Condition[ConditionFlag.OccupiedInCutSceneEvent] || Service.Condition[ConditionFlag.WatchingCutscene78])
        {
            TaskManager.DelayNext(100);
            TaskManager.Enqueue(IsWatchingCutscene);
            return true;
        }

        AbortActions();
        return true;
    }

    private static unsafe void AbortActions()
    {
        if (TryGetAddonByName<AtkUnitBase>("SystemMenu", out var menu) && HelpersOm.IsAddonAndNodesReady(menu))
        {
            AddonManager.Callback(menu, true, -1);
            menu->Hide(true);
        }

        TaskManager?.Abort();
    }

    public void Uninit()
    {
        Service.Condition.ConditionChange -= OnConditionChanged;
        Service.Toast.ErrorToast -= OnErrorToast;
        AbortActions();
    }
}
