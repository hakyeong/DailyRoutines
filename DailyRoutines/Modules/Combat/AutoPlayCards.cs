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
public unsafe class AutoPlayCards : DailyModuleBase
{
    private delegate bool UseActionSelfDelegate(
        ActionManager* actionManager, uint actionType, uint actionID, ulong targetID = 0xE000_0000, uint a4 = 0,
        uint a5 = 0, uint a6 = 0, void* a7 = null);

    private Hook<UseActionSelfDelegate>? useActionSelfHook;

    private static readonly HashSet<uint> CardStatuses = [1882, 1883, 1884, 1885, 1886, 1887];
    private static readonly HashSet<uint> MeleeCardStatuses = [913, 915, 916]; // 近战卡
    private static readonly HashSet<uint> RangeCardStatuses = [914, 917, 918]; // 远程卡

    private static bool ConfigSendMessage = true;

    public override void Init()
    {
        useActionSelfHook =
            Service.Hook.HookFromAddress<UseActionSelfDelegate>((nint)ActionManager.MemberFunctionPointers.UseAction,
                                                                UseActionSelf);
        useActionSelfHook?.Enable();

        AddConfig(this, "SendMessage", ConfigSendMessage);
        ConfigSendMessage = GetConfig<bool>(this, "SendMessage");
    }

    public override void ConfigUI()
    {
        if (ImGui.Checkbox(Service.Lang.GetText("AutoPlayCards-SendMessage"), ref ConfigSendMessage))
            UpdateConfig(this, "SendMessage", ConfigSendMessage);
    }

    private bool UseActionSelf(
        ActionManager* actionManager, uint actionType, uint actionID, ulong targetID, uint a4, uint a5, uint a6,
        void* a7)
    {
        if (actionType != 1 || actionID != 17055)
            return useActionSelfHook.Original(actionManager, actionType, actionID, targetID, a4, a5, a6, a7);

        var localPlayer = Service.ClientState.LocalPlayer;

        bool isMeleeCardDrawn = MeleeCardStatuses.Any(
                 x => localPlayer.BattleChara()->GetStatusManager->HasStatus(x)),
             isRangeCardDrawn = RangeCardStatuses.Any(
                 x => localPlayer.BattleChara()->GetStatusManager->HasStatus(x));
        if (!isMeleeCardDrawn && !isRangeCardDrawn)
            return useActionSelfHook.Original(actionManager, actionType, actionID, targetID, a4, a5, a6, a7);

        var member = Service.PartyList
                            .Where(x => x.GameObject != null && x.GameObject.IsValid() && 
                                        x.GameObject.IsTargetable && !x.GameObject.IsDead)
                            .Select(x => new
                            {
                                Member = x,
                                Distance = HelpersOm.GetGameDistanceFromObject(
                                    (GameObject*)localPlayer.Address,
                                    (GameObject*)x.GameObject.Address)
                            })
                            .Where(x => !x.Member.Statuses.Any(s => CardStatuses.Contains(s.StatusId))
                                        && x.Distance <= 30)
                            .OrderByDescending(x => x.Member.ClassJob.GameData.Role is 2 or 3 ? 1 : 0)
                            .ThenByDescending(x => x.Member.ClassJob.GameData.Role is 1 or 2 ? isMeleeCardDrawn ? 1 : 0 :
                                                   isMeleeCardDrawn ? 0 : 1)
                            .Select(x => x.Member)
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
}
