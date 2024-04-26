using DailyRoutines.Managers;
using Dalamud.Game.ClientState.Conditions;

namespace DailyRoutines.Infos;

public static class Flags
{
    public static bool OccupiedInEvent() => Service.Condition[ConditionFlag.Occupied]
               || Service.Condition[ConditionFlag.Occupied30]
               || Service.Condition[ConditionFlag.Occupied33]
               || Service.Condition[ConditionFlag.Occupied38]
               || Service.Condition[ConditionFlag.Occupied39]
               || Service.Condition[ConditionFlag.OccupiedInCutSceneEvent]
               || Service.Condition[ConditionFlag.OccupiedInEvent]
               || Service.Condition[ConditionFlag.OccupiedInQuestEvent]
               || Service.Condition[ConditionFlag.OccupiedSummoningBell]
               || Service.Condition[ConditionFlag.WatchingCutscene]
               || Service.Condition[ConditionFlag.WatchingCutscene78]
               || Service.Condition[ConditionFlag.BetweenAreas]
               || Service.Condition[ConditionFlag.BetweenAreas51]
               || Service.Condition[ConditionFlag.InThatPosition]
               || Service.Condition[ConditionFlag.TradeOpen]
               || Service.Condition[ConditionFlag.Crafting]
               || Service.Condition[ConditionFlag.InThatPosition]
               || Service.Condition[ConditionFlag.Unconscious]
               || Service.Condition[ConditionFlag.MeldingMateria]
               || Service.Condition[ConditionFlag.Gathering]
               || Service.Condition[ConditionFlag.OperatingSiegeMachine]
               || Service.Condition[ConditionFlag.CarryingItem]
               || Service.Condition[ConditionFlag.CarryingObject]
               || Service.Condition[ConditionFlag.BeingMoved]
               || Service.Condition[ConditionFlag.Emoting]
               || Service.Condition[ConditionFlag.Mounted2]
               || Service.Condition[ConditionFlag.Mounting]
               || Service.Condition[ConditionFlag.Mounting71]
               || Service.Condition[ConditionFlag.ParticipatingInCustomMatch]
               || Service.Condition[ConditionFlag.PlayingLordOfVerminion]
               || Service.Condition[ConditionFlag.ChocoboRacing]
               || Service.Condition[ConditionFlag.PlayingMiniGame]
               || Service.Condition[ConditionFlag.Performing]
               || Service.Condition[ConditionFlag.PreparingToCraft]
               || Service.Condition[ConditionFlag.Fishing]
               || Service.Condition[ConditionFlag.Transformed]
               || Service.Condition[ConditionFlag.UsingHousingFunctions]
               || Service.ClientState.LocalPlayer?.IsTargetable != true;

    public static bool BetweenAreas() => Service.Condition[ConditionFlag.BetweenAreas] || Service.Condition[ConditionFlag.BetweenAreas51];


    public static bool BoundByDuty() => Service.Condition[ConditionFlag.BoundByDuty] ||
                                        Service.Condition[ConditionFlag.BoundByDuty56] ||
                                        Service.Condition[ConditionFlag.BoundByDuty95] ||
                                        Service.Condition[ConditionFlag.InDutyQueue];

    public static bool IsCasting => Service.Condition[ConditionFlag.Casting] || Service.Condition[ConditionFlag.Casting87];
    public static bool IsOnMount => Service.Condition[ConditionFlag.Mounted] || Service.Condition[ConditionFlag.Mounted2];
    public static bool CanMount => !Service.Condition[ConditionFlag.Mounted] && !Service.Condition[ConditionFlag.Mounting] && !Service.Condition[ConditionFlag.InCombat] && !Service.Condition[ConditionFlag.Casting];


}
