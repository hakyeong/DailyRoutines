using DailyRoutines.Helpers;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using GameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoCACTitle", "AutoCACDescription", ModuleCategories.金碟)]
public unsafe class AutoPunchingMachine : DailyModuleBase
{
    private delegate nint AgentMiniGameReceiveEventDelegate(AgentInterface* agent, nint a2, nint a3, uint a4, int a5);
    [Signature("48 89 74 24 ?? 48 89 7C 24 ?? 41 56 48 83 EC ?? 48 8B F9 45 8B D9")]
    private static AgentMiniGameReceiveEventDelegate? AgentMiniGameReceiveEvent;

    private delegate void GameSuccessDelegate(AgentInterface* agent, int a2);
    [Signature("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? BA ?? ?? ?? ?? 49 8B CE E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 41 80 BE ?? ?? ?? ?? ?? 0F 84")]
    private static GameSuccessDelegate? GameSuccess;

    public override void Init()
    {
        Service.Hook.InitializeFromAttributes(this);

        TaskHelper ??= new TaskHelper();
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "PunchingMachine", OnAddonSetup);
    }

    public override void ConfigUI() { ConflictKeyText(); }

    private void OnAddonSetup(AddonEvent type, AddonArgs args)
    {
        if (InterruptByConflictKey()) return;

        TaskHelper.Enqueue(() =>
        {
            if (!args.Addon.ToAtkUnitBase()->IsVisible) return false;

            var agent = AgentModule.Instance()->GetAgentByInternalId(AgentId.GoldSaucerMiniGame);
            var a2 = 1;
            var a3 = 0;
            const uint a4 = 1U;
            const int a5 = 2;
            AgentMiniGameReceiveEvent(agent, (nint)(&a2), (nint)(&a3), a4, a5);
            return true;
        });

        TaskHelper.DelayNext(400);
        TaskHelper.Enqueue(() =>
        {
            var agent = AgentModule.Instance()->GetAgentByInternalId(AgentId.GoldSaucerMiniGame);
            GameSuccess(agent, 16);
        });

        TaskHelper.Enqueue(StartAnotherRound);
    }

    private bool? StartAnotherRound()
    {
        if (InterruptByConflictKey()) return true;

        if (Flags.OccupiedInEvent) return false;
        var machineTarget = Service.Target.PreviousTarget;
        var machine = machineTarget.Name.TextValue.Contains("重击伽美什") ? (GameObject*)machineTarget.Address : null;

        if (machine != null)
        {
            TargetSystem.Instance()->InteractWithObject(machine);
            return true;
        }

        return false;
    }

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddonSetup);

        base.Uninit();
    }
}
