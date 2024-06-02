using System;
using System.Collections.Generic;
using DailyRoutines.Helpers;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using ECommons.Automation.LegacyTaskManager;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoCheckItemLevelTitle", "AutoCheckItemLevelDescription", ModuleCategories.战斗)]
public unsafe class AutoCheckItemLevel : DailyModuleBase
{
    private static readonly HashSet<uint> ValidContentJobCategories = [108, 142, 146];
    private static readonly HashSet<uint> HaveOffHandJobCategories = [2, 7, 8, 20];

    public override void Init()
    {
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 20000, ShowDebug = false };

        Service.ClientState.TerritoryChanged += OnZoneChanged;
    }

    private void OnZoneChanged(ushort zone)
    {
        if (Service.ClientState.IsPvP) return;
        if (!PresetData.TryGetContent(zone, out var content) || content.PvP ||
            !ValidContentJobCategories.Contains(content.AcceptClassJobCategory.Row)) return;

        Service.Framework.RunOnTick(CheckMembersItemLevel, TimeSpan.FromMilliseconds(500));
    }

    private void CheckMembersItemLevel()
    {
        TaskManager.Enqueue(() => TryGetAddonByName<AtkUnitBase>("NowLoading", out var addon) && !addon->IsVisible);
        TaskManager.Enqueue(() =>
        {
            var content = PresetData.Contents[Service.ClientState.TerritoryType];
            var ssb = new SeStringBuilder();
            ssb.Append($"{Service.Lang.GetText("AutoCheckItemLevel-ILRequired")}: ")
               .AddUiForeground(content.ItemLevelRequired.ToString(), 34);

            Service.Chat.Print(ssb.Build());
        });

        TaskManager.Enqueue(() =>
        {
            foreach (var member in Service.PartyList)
            {
                if (member.ObjectId == Service.ClientState.LocalPlayer.ObjectId) continue;

                TaskManager.Enqueue(() =>
                {
                    if (!EzThrottler.Throttle("AutoCheckItemLevel-WaitExamineUI", 1000)) return false;
                    AgentInspect.Instance()->ExamineCharacter(member.ObjectId);
                    return Service.Gui.GetAddonByName("CharacterInspect") != nint.Zero;
                });

                TaskManager.Enqueue(() =>
                {
                    if (!EzThrottler.Throttle("AutoCheckItemLevel-CheckCharacterIL", 1000)) return false;
                    if (InterruptByConflictKey()) return true;
                    if (!Flags.BoundByDuty)
                    {
                        TaskManager.Abort();
                        return true;
                    }

                    if (!TryGetAddonByName<AtkUnitBase>("CharacterInspect", out var addon) ||
                        !IsAddonAndNodesReady(addon) || AgentInspect.Instance()->CurrentObjectID != member.ObjectId)
                    {
                        AgentInspect.Instance()->ExamineCharacter(member.ObjectId);
                        return false;
                    }

                    var container = InventoryManager.Instance()->GetInventoryContainer(InventoryType.Examine);
                    if (container == null)
                    {
                        AgentInspect.Instance()->ExamineCharacter(member.ObjectId);
                        return false;
                    }

                    uint totalIL = 0U, lowestIL = uint.MaxValue;
                    var itemSlotAmount = 11;
                    for (var i = 0; i < 13; i++)
                    {
                        if (i == 0)
                        {
                            var mainHand = LuminaCache.GetRow<Item>(container->GetInventorySlot(i)->ItemID);
                            var category = mainHand.ClassJobCategory.Row;
                            if (HaveOffHandJobCategories.Contains(category))
                                itemSlotAmount++;
                        }

                        if (i == 1 && itemSlotAmount != 12) continue;

                        // 腰带
                        if (i == 5) continue;

                        var slot = container->GetInventorySlot(i);
                        if (slot == null) continue;

                        var itemID = slot->ItemID;
                        var item = LuminaCache.GetRow<Item>(itemID);

                        if (item.LevelItem.Row < lowestIL)
                            lowestIL = item.LevelItem.Row;

                        totalIL += item.LevelItem.Row;
                    }

                    var avgItemLevel = totalIL / itemSlotAmount;

                    var content = PresetData.Contents[Service.ClientState.TerritoryType];
                    var ssb = new SeStringBuilder();
                    ssb.AddUiForeground(25);
                    ssb.Add(new PlayerPayload(member.Name.TextValue, member.World.Id));
                    ssb.AddUiForegroundOff();
                    ssb.Append($" ({member.ClassJob.GameData.Name.RawString})");
                    ssb.Append($" {Service.Lang.GetText("Level")}: ").AddUiForeground(member.Level.ToString(),
                        (ushort)(member.Level >= content.ClassJobLevelSync ? 43 : 17));

                    ssb.Add(new NewLinePayload());
                    ssb.Append($" {Service.Lang.GetText("AutoCheckItemLevel-ILAverage")}: ")
                       .AddUiForeground(avgItemLevel.ToString(), (ushort)(avgItemLevel > content.ItemLevelSync ? 43 : 17));

                    ssb.Append($" {Service.Lang.GetText("AutoCheckItemLevel-ILMinimum")}: ")
                       .AddUiForeground(lowestIL.ToString(), (ushort)(lowestIL > content.ItemLevelRequired ? 43 : 17));

                    ssb.Add(new NewLinePayload());

                    Service.Chat.Print(ssb.Build());

                    AgentInspect.Instance()->AgentInterface.Hide();
                    return true;
                });
            }
        });
    }

    public override void Uninit()
    {
        Service.ClientState.TerritoryChanged -= OnZoneChanged;

        base.Uninit();
    }
}
