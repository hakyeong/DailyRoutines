using System.Collections.Generic;
using System.Linq;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Hooking;
using ECommons.GameFunctions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoPlayCardsTitle", "AutoPlayCardsDescription", ModuleCategories.Combat)]
public unsafe class AutoPlayCards : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithConfigUI => true;

    private delegate bool UseActionSelfDelegate(
        ActionManager* actionManager, uint actionType, uint actionID, ulong targetID = 0xE000_0000, uint a4 = 0,
        uint a5 = 0, uint a6 = 0, void* a7 = null);

    private Hook<UseActionSelfDelegate>? useActionSelfHook;

    private static readonly HashSet<uint> CardStatuses = [1882, 1883, 1884, 1885, 1886, 1887];
    private static readonly HashSet<uint> MeleeCardStatuses = [913, 915, 916]; // 近战卡
    private static readonly HashSet<uint> RangeCardStatuses = [914, 917, 918]; // 远程卡

    private static bool ConfigSendMessage = true;

    public void Init()
    {
        useActionSelfHook =
            Service.Hook.HookFromAddress<UseActionSelfDelegate>((nint)ActionManager.MemberFunctionPointers.UseAction,
                                                                UseActionSelf);
        useActionSelfHook?.Enable();

        Service.Config.AddConfig(this, "SendMessage", ConfigSendMessage);
        ConfigSendMessage = Service.Config.GetConfig<bool>(this, "SendMessage");
    }

    public void ConfigUI()
    {
        if (ImGui.Checkbox(Service.Lang.GetText("AutoPlayCards-SendMessage"), ref ConfigSendMessage))
            Service.Config.UpdateConfig(this, "SendMessage", ConfigSendMessage);
    }

    public void OverlayUI() { }

    private bool UseActionSelf(
        ActionManager* actionManager, uint actionType, uint actionID, ulong targetID, uint a4, uint a5, uint a6,
        void* a7)
    {
        if (actionID != 17055)
            return useActionSelfHook.Original(actionManager, actionType, actionID, targetID, a4, a5, a6, a7);

        bool isMeleeCardDrawn = MeleeCardStatuses.Any(
                 x => Service.ClientState.LocalPlayer.BattleChara()->GetStatusManager->HasStatus(x)),
             isRangeCardDrawn = RangeCardStatuses.Any(
                 x => Service.ClientState.LocalPlayer.BattleChara()->GetStatusManager->HasStatus(x));
        if (!isMeleeCardDrawn && !isRangeCardDrawn)
            return useActionSelfHook.Original(actionManager, actionType, actionID, targetID, a4, a5, a6, a7);

        var member = Service.PartyList
                            .Where(x => x.GameObject.IsTargetable && !x.GameObject.IsDead)
                            .Select(x => new
                            {
                                x,
                                Distance = HelpersOm.GetGameDistanceFromObject(
                                    (GameObject*)Service.ClientState.LocalPlayer.Address,
                                    (GameObject*)x.GameObject.Address)
                            })
                            .Where(x => !x.x.Statuses.Any(s => CardStatuses.Contains(s.StatusId)) && x.Distance <= 30)
                            .OrderByDescending(x => x.x.ClassJob.GameData.Role is 2 or 3 ? 1 : 0)
                            .ThenByDescending(x => x.x.ClassJob.GameData.Role is 1 or 2 ? isMeleeCardDrawn ? 1 : 0 :
                                                   isMeleeCardDrawn ? 0 : 1)
                            .Select(x => x.x)
                            .FirstOrDefault();
        if (member == null)
            return useActionSelfHook.Original(actionManager, actionType, actionID, targetID, a4, a5, a6, a7);

        var state = useActionSelfHook.Original(actionManager, actionType, actionID, member.ObjectId, a4, a5, a6, a7);
        if (ConfigSendMessage && state)
        {
            string cardTypeText =
                       Service.Lang.GetText(isMeleeCardDrawn ? "AutoPlayCards-Melee" : "AutoPlayCards-Range"),
                   jobNameText = member.ClassJob.GameData.Name.ExtractText(),
                   memberNameText = member.Name.ExtractText();
            Service.Chat.Print(Service.Lang.GetText("AutoPlayCards-Message", cardTypeText, jobNameText,
                                                    memberNameText));
        }

        return state;
    }

    public void Uninit()
    {
        useActionSelfHook?.Dispose();
    }
}
