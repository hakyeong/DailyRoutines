using System.Windows.Forms;
using ClickLib;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.AddonLifecycle;
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

    private static bool IsInCutScene;

    public void Init()
    {
        TaskManager ??= new TaskManager { TimeLimitMS = int.MaxValue, ShowDebug = false };
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, "NowLoading", OnAddonDrawn);
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
                IsInCutScene = true;
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

    private static void OnAddonDrawn(AddonEvent type, AddonArgs args)
    {
        if (Service.KeyState[Service.Config.ConflictKey]) return;

        if ((Service.Condition[ConditionFlag.OccupiedInCutSceneEvent] ||
             Service.Condition[ConditionFlag.WatchingCutscene78]) && IsInCutScene)
            TaskManager.Enqueue(IsWatchingCutscene);
    }

    private static unsafe bool? IsWatchingCutscene()
    {
        WindowsKeypress.SendKeypress(Keys.Escape);

        if (TryGetAddonByName<AtkUnitBase>("SystemMenu", out var menu) && HelpersOm.IsAddonAndNodesReady(menu))
        {
            AddonManager.Callback(menu, true, -1);
            menu->Hide(true);
        }

        if (TryGetAddonByName<AtkUnitBase>("SelectString", out var addon) && HelpersOm.IsAddonAndNodesReady(addon))
        {
            if (addon->GetTextNodeById(2)->NodeText.ExtractText().Contains("要跳过这段过场动画吗"))
            {
                Click.SendClick("select_string1");
            }
        }

        if (!Service.Condition[ConditionFlag.OccupiedInCutSceneEvent] &&
            !Service.Condition[ConditionFlag.WatchingCutscene78])
        {
            AbortActions();
            return true;
        }

        return false;
    }

    private static unsafe void AbortActions()
    {
        if (TryGetAddonByName<AtkUnitBase>("SystemMenu", out var menu) && HelpersOm.IsAddonAndNodesReady(menu))
        {
            AddonManager.Callback(menu, true, -1);
            menu->Hide(true);
        }

        IsInCutScene = false;
        TaskManager?.Abort();
        Service.Toast.ErrorToast -= OnErrorToast;
    }

    public void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddonDrawn);
        Service.Condition.ConditionChange -= OnConditionChanged;
        AbortActions();
    }
}
