using ClickLib.Bases;
using ClickLib.Clicks;
using DailyRoutines.Clicks;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.AddonLifecycle;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoRequestItemSubmitTitle", "AutoRequestItemSubmitDescription", ModuleCategories.General)]
public class AutoRequestItemSubmit : IDailyModule
{
    public bool Initialized { get; set; }

    private TaskManager? TaskManager;

    public void Init()
    {
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = false };
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "Request", OnAddonSetup);

        Initialized = true;
    }

    public void UI() { }

    private void OnAddonSetup(AddonEvent eventType, AddonArgs addonInfo)
    {
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "SelectYesno", OnAddonHqConfirm);
        TaskManager.Enqueue(ClickRequestIcon);
        TaskManager.Enqueue(ClickItemToSelect);
        TaskManager.Enqueue(ClickHandOver);
        TaskManager.Enqueue(ClickHqSubmit);
        Service.AddonLifecycle.UnregisterListener(OnAddonHqConfirm);
    }

    private void OnAddonHqConfirm(AddonEvent eventType, AddonArgs addonInfo)
    {
        TaskManager.Enqueue(ClickHqSubmit);
    }

    private static unsafe bool? ClickRequestIcon()
    {
        if (TryGetAddonByName<AddonRequest>("Request", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(&addon->AtkUnitBase))
        {
            var ui = &addon->AtkUnitBase;
            var handler = new ClickRequestDR();

            handler.Click();
            return true;
        }

        return false;
    }

    private static unsafe bool? ClickItemToSelect()
    {
        if (TryGetAddonByName<AddonContextIconMenu>("ContextIconMenu", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(&addon->AtkUnitBase))
        {
            var ui = &addon->AtkUnitBase;
            var handler = new ClickContextIconMenuDR();
            var imageNode =
                addon->AtkComponentList240->AtkComponentBase.UldManager.NodeList[1]->GetAsAtkComponentNode()->Component
                        ->UldManager.NodeList[1]->GetAsAtkComponentNode()->Component->UldManager
                    .NodeList[0]->GetAsAtkImageNode();
            var iconId = imageNode->PartsList->Parts[imageNode->PartId].UldAsset->AtkTexture.Resource->IconID;
            handler.ClickItem((ushort)iconId, true);
            handler.ClickItem((ushort)iconId, false);
            return true;
        }

        return false;
    }

    private static unsafe bool? ClickHandOver()
    {
        if (TryGetAddonByName<AddonRequest>("Request", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(&addon->AtkUnitBase))
        {
            var handler = new ClickRequest();

            handler.HandOver();
            return true;
        }

        return false;
    }

    private static unsafe bool? ClickHqSubmit()
    {
        if (TryGetAddonByName<AddonSelectYesno>("SelectYesno", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(&addon->AtkUnitBase))
        {
            var handler = new ClickSelectYesNo();

            handler.Yes();
            return true;
        }

        return false;
    }

    public void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddonSetup);
        Service.AddonLifecycle.UnregisterListener(OnAddonHqConfirm);
        TaskManager?.Abort();

        Initialized = false;
    }
}
