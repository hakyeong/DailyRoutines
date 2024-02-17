using System.Threading.Tasks;
using System.Windows.Forms;
using ClickLib;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.AddonLifecycle;
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

    public void Init()
    {
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

                Task.Delay(10)
                    .ContinueWith(
                        _ => Service.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, "NowLoading",
                                                                     OnAddonLoading));
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

    private static unsafe void OnAddonLoading(AddonEvent type, AddonArgs args)
    {
        WindowsKeypress.SendKeypress(Keys.Escape);
        if (TryGetAddonByName<AtkUnitBase>("SystemMenu", out var menu) && IsAddonReady(menu))
        {
            Callback.Fire(menu, true, -1);
            menu->Hide(true);
            AbortActions();
            return;
        }

        if (TryGetAddonByName<AtkUnitBase>("SelectString", out var addon) && IsAddonReady(addon))
        {
            if (addon->GetTextNodeById(2)->NodeText.ExtractText().Contains("要跳过这段过场动画吗"))
                if (Click.TrySendClick("select_string1"))
                    AbortActions();
        }
    }

    private static void AbortActions()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddonLoading);
        Service.Toast.ErrorToast -= OnErrorToast;
    }

    public void Uninit()
    {
        Service.Condition.ConditionChange -= OnConditionChanged;
        AbortActions();
    }
}
