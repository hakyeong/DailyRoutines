using System.Collections.Generic;
using System.Linq;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Hooking;
using ECommons.GameFunctions;
using FFXIVClientStructs.FFXIV.Client.Game;
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
            Hook<UseActionSelfDelegate>.FromAddress((nint)ActionManager.MemberFunctionPointers.UseAction,
                                                    UseActionSelf);
        useActionSelfHook?.Enable();

        Service.Config.AddConfig(this, "SendMessage", ConfigSendMessage);
        ConfigSendMessage = Service.Config.GetConfig<bool>(this, "SendMessage");
    }

    public void ConfigUI()
    {
        if (ImGui.Checkbox(Service.Lang.GetText("AutoPlayCards-SendMessage"), ref ConfigSendMessage))
        {
            Service.Config.UpdateConfig(this, "SendMessage", ConfigSendMessage);
        }
    }

    public void OverlayUI() { }

    private bool UseActionSelf(ActionManager* actionManager, uint actionType, uint actionID, ulong targetID, uint a4, uint a5, uint a6, void* a7)
    {
        if (actionID == 17055)
        {
            var isMeleeCardDrawn = MeleeCardStatuses.Any(x => Service.ClientState.LocalPlayer.BattleChara()->GetStatusManager->HasStatus(x)) ;
            var member = Service.PartyList
                                .Where(x => x.GameObject.IsTargetable && !x.GameObject.IsDead && !x.Statuses.Any(s => CardStatuses.Contains(s.StatusId)))
                                .OrderByDescending(x =>
                                {
                                    return x.ClassJob.GameData.Role switch
                                    {
                                        2 or 3 => 1,
                                        1 or 4 => 0,
                                        _ => 0
                                    };
                                })
                                .ThenByDescending(x =>
                                {
                                    return x.ClassJob.GameData.Role switch
                                    {
                                        1 or 2 => isMeleeCardDrawn ? 1 : 0,
                                        3 or 4 => isMeleeCardDrawn ? 0 : 1,
                                        _ => 0
                                    };
                                })
                                .FirstOrDefault();
            if (member == null) return useActionSelfHook.Original(actionManager, actionType, actionID, targetID, a4, a5, a6, a7);

            if (ConfigSendMessage)
                Service.Chat.Print(Service.Lang.GetText("AutoPlayCards-Message", Service.Lang.GetText(isMeleeCardDrawn ? "AutoPlayCards-Melee" : "AutoPlayCards-Range"), member.ClassJob.GameData.Name.ExtractText(), member.Name.ExtractText()));
            return useActionSelfHook.Original(actionManager, actionType, actionID, member.ObjectId, a4, a5, a6, a7);
        }

        return useActionSelfHook.Original(actionManager, actionType, actionID, targetID, a4, a5, a6, a7);
    }

    public void Uninit()
    {
        useActionSelfHook?.Dispose();
    }
}
