using DailyRoutines.Infos;
using DailyRoutines.Infos.Clicks;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Interface.Colors;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoCollectableExchangeTitle", "AutoCollectableExchangeDescription", ModuleCategories.界面操作)]
public unsafe class AutoCollectableExchange : DailyModuleBase
{
    public override void Init()
    {
        TaskHelper ??= new() { AbortOnTimeout = true };
        Overlay ??= new(this);

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "CollectablesShop", OnAddon);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "CollectabsleShop", OnAddon);
    }

    public override void OverlayUI()
    {
        if (AddonState.CollectablesShop == null)
        {
            Overlay.IsOpen = false;
            return;
        }
        var buttonNode = AddonState.CollectablesShop->GetNodeById(51);
        if (buttonNode == null);

        ImGui.SetWindowPos(new(buttonNode->ScreenX - ImGui.GetWindowSize().X, buttonNode->ScreenY + 4f));

        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(ImGuiColors.DalamudYellow, Service.Lang.GetText("AutoCollectableExchangeTitle"));

        ImGui.SameLine();
        ImGui.BeginDisabled(TaskHelper.IsBusy);
        if (ImGui.Button(Service.Lang.GetText("Start"))) EnqueueExchange();
        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("Stop"))) TaskHelper.Abort();
    }

    private void EnqueueExchange()
    {
        TaskHelper.Enqueue(() =>
        {
            if (AddonState.CollectablesShop == null)
            {
                TaskHelper.Abort();
                return true;
            }

            var list = (AtkComponentList*)AddonState.CollectablesShop->GetComponentNodeById(31);
            if (list == null) return false;

            if (list->ListLength <= 0)
            {
                TaskHelper.Abort();
                return true;
            }
            ClickCollectableShop.Using((nint)AddonState.CollectablesShop).Exchange();
            return true;
        }, "ClickExchange");

        TaskHelper.Enqueue(() =>
        {
            TaskHelper.DelayNext("Delay_EnqueueNewRound",100, false, 2);
            TaskHelper.Enqueue(EnqueueExchange, "EnqueueNewRound");
        });
    }

    private void OnAddon(AddonEvent type, AddonArgs args)
    {
        Overlay.IsOpen = type switch
        {
            AddonEvent.PostSetup => true,
            AddonEvent.PreFinalize => false,
            _ => Overlay.IsOpen,
        };
    }

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddon);

        base.Uninit();
    }
}
