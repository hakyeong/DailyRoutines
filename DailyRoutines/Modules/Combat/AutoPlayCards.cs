using System;
using System.Collections.Generic;
using System.Linq;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.ClientState.JobGauge.Enums;
using Dalamud.Game.ClientState.JobGauge.Types;
using Dalamud.Game.ClientState.Party;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using ImGuiNET;
using Action = Lumina.Excel.GeneratedSheets.Action;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoPlayCardsTitle", "AutoPlayCardsDescription", ModuleCategories.Combat)]
public unsafe class AutoPlayCards : DailyModuleBase
{
    private delegate bool UseActionSelfDelegate(
        ActionManager* actionManager, uint actionType, uint actionID, ulong targetID = 0xE000_0000, uint a4 = 0,
        uint a5 = 0, uint a6 = 0, void* a7 = null);

    private Hook<UseActionSelfDelegate>? useActionSelfHook;

    private static readonly HashSet<uint> CardStatuses = [1882, 1883, 1884, 1885, 1886, 1887];

    private static readonly Dictionary<CardType, (bool IsMelee, string Name)> Cards = new()
    {
        { CardType.BALANCE, (true, LuminaCache.GetRow<Action>(4401).Name.RawString) },
        { CardType.ARROW, (true, LuminaCache.GetRow<Action>(4402).Name.RawString) },
        { CardType.SPEAR, (true, LuminaCache.GetRow<Action>(4403).Name.RawString) },
        { CardType.BOLE, (false, LuminaCache.GetRow<Action>(4403).Name.RawString) },
        { CardType.EWER, (false, LuminaCache.GetRow<Action>(4403).Name.RawString) },
        { CardType.SPIRE, (false, LuminaCache.GetRow<Action>(4403).Name.RawString) }
    };

    private static bool SendMessage = true;
    private static bool UseAantonomasia;

    public override void Init()
    {
        useActionSelfHook =
            Service.Hook.HookFromAddress<UseActionSelfDelegate>((nint)ActionManager.MemberFunctionPointers.UseAction,
                                                                UseActionSelf);
        useActionSelfHook?.Enable();

        AddConfig(this, "SendMessage", SendMessage);
        SendMessage = GetConfig<bool>(this, "SendMessage");

        AddConfig(this, "UseAantonomasia", UseAantonomasia);
        UseAantonomasia = GetConfig<bool>(this, "UseAantonomasia");
    }

    public override void ConfigUI()
    {
        if (ImGui.Checkbox(Service.Lang.GetText("AutoPlayCards-SendMessage"), ref SendMessage))
            UpdateConfig(this, "SendMessage", SendMessage);

        if (SendMessage)
        {
            ImGui.Indent();
            if (ImGui.Checkbox(Service.Lang.GetText("AutoPlayCards-UseAantonomasia"), ref UseAantonomasia))
                UpdateConfig(this, "UseAantonomasia", UseAantonomasia);

            ImGuiOm.HelpMarker(Service.Lang.GetText("AutoPlayCards-UseAantonomasiaHelp"));
            ImGui.Unindent();
        }
    }

    private bool UseActionSelf(ActionManager* actionManager, uint actionType, uint actionID, ulong targetID, uint a4, uint a5, uint a6, void* a7)
    {
        if (actionType != 1 || actionID != 17055)
            return useActionSelfHook.Original(actionManager, actionType, actionID, targetID, a4, a5, a6, a7);

        var localPlayer = Service.ClientState.LocalPlayer;
        var drawnCard = Service.JobGauges.Get<ASTGauge>().DrawnCard;

        if (!Cards.TryGetValue(drawnCard, out var cardInfo))
            return useActionSelfHook.Original(actionManager, actionType, actionID, targetID, a4, a5, a6, a7);

        var indices = Enumerable.Range(0, Service.PartyList.Count)
                                .Where(i => Service.PartyList[i].GameObject != null && 
                                            Service.PartyList[i].GameObject.IsValid() && 
                                            Service.PartyList[i].GameObject.IsTargetable && 
                                            !Service.PartyList[i].GameObject.IsDead &&
                                            !Service.PartyList[i].Statuses.Any(s => CardStatuses.Contains(s.StatusId)) && 
                                            GetGameDistanceFromObject((GameObject*)localPlayer.Address, (GameObject*)Service.PartyList[i].GameObject.Address) <= 30)
                                .ToList();

        PartyMember? member = null;
        if (indices.Count > 0)
        {
            var rnd = new Random();
            for (var i = indices.Count - 1; i >= 0; i--)
            {
                var j = rnd.Next(i + 1);
                (indices[i], indices[j]) = (indices[j], indices[i]);
            }

            var firstSortedIndex = 
                indices.OrderByDescending(index => Service.PartyList[index].ClassJob.GameData.Role is 2 or 3 ? 1 : 0)
                       .ThenByDescending(index =>
                         Service.PartyList[index].ClassJob.GameData.Role is 1 or 2 ? 
                            cardInfo.IsMelee ? 1 : 0 :
                            cardInfo.IsMelee ? 0 : 1
            ).First();

            member = Service.PartyList[firstSortedIndex];
        }

        if (member == null)
            return useActionSelfHook.Original(actionManager, actionType, actionID, targetID, a4, a5, a6, a7);

        var state = useActionSelfHook.Original(actionManager, actionType, actionID, member.ObjectId, a4, a5, a6, a7);
        if (SendMessage && state)
        {
            string jobNameText = member.ClassJob.GameData.Name.ExtractText(),
                   memberNameText = member.Name.ExtractText();

            var message = new SeStringBuilder().Append(DRPrefix()).Append(" ").Append(Service.Lang.GetSeString("AutoPlayCards-Message", UseAantonomasia ? cardInfo.IsMelee ? Service.Lang.GetText("AutoPlayCards-MeleeCard") : Service.Lang.GetText("AutoPlayCards-RangeCard") : cardInfo.Name, member.ClassJob.GameData.ToBitmapFontIcon(), jobNameText, memberNameText)).Build();
            Service.Chat.Print(message);
        }

        return state;
    }
}
