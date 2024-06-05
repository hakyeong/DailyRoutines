using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using ClickLib;
using ClickLib.Clicks;
using DailyRoutines.Helpers;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using DailyRoutines.Windows;
using Dalamud;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Hooking;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Memory;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using ECommons.Automation.LegacyTaskManager;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;

namespace DailyRoutines.Modules;

[ModuleDescription("FastObjectInteractTitle", "FastObjectInteractDescription", ModuleCategories.界面优化)]
public unsafe partial class FastObjectInteract : DailyModuleBase
{
    private delegate nint AgentWorldTravelReceiveEventDelegate(
        AgentWorldTravel* agent, nint a2, nint a3, nint a4, long eventCase);
    [Signature("40 55 53 56 57 41 54 41 56 41 57 48 8D AC 24 ?? ?? ?? ?? B8",
               DetourName = nameof(AgentWorldTravelReceiveEventDetour))]
    private static Hook<AgentWorldTravelReceiveEventDelegate>? AgentWorldTravelReceiveEventHook;


    private static readonly Dictionary<ObjectKind, string> ObjectKindLoc = new()
    {
        { ObjectKind.BattleNpc, "战斗类 NPC (不建议)" },
        { ObjectKind.EventNpc, "一般类 NPC" },
        { ObjectKind.EventObj, "事件物体 (绝大多数要交互的都属于此类)" },
        { ObjectKind.Treasure, "宝箱" },
        { ObjectKind.Aetheryte, "以太之光" },
        { ObjectKind.GatheringPoint, "采集点" },
        { ObjectKind.MountType, "坐骑 (不建议)" },
        { ObjectKind.Companion, "宠物 (不建议)" },
        { ObjectKind.Retainer, "雇员" },
        { ObjectKind.Area, "地图传送相关" },
        { ObjectKind.Housing, "家具庭具" },
        { ObjectKind.CardStand, "固定类物体 (如无人岛采集点等)" },
        { ObjectKind.Ornament, "时尚配饰 (不建议)" },
    };

    private const string ENPCTiltleText = "[{0}] {1}";
    private static Dictionary<uint, string>? ENpcTitles;
    private static HashSet<uint>? ImportantENPC;

    private static Config ModuleConfig = null!;
    private static EzThrottler<string> MonitorThrottler = new();
    private static EzThrottler<nint> ObjectsThrottler = new();

    private static string BlacklistKeyInput = string.Empty;
    private static float WindowWidth;

    private static readonly List<ObjectToSelect> tempObjects = new(596);
    private static TargetSystem* TargetSystem;
    private static readonly Dictionary<nint, ObjectToSelect> ObjectsToSelect = [];

    private static string AethernetShardName = string.Empty;
    private static bool IsInInstancedArea;
    private static int InstancedAreaAmount = 3;

    private static HashSet<uint> WorldTravelValidZones = [132, 129, 130];
    private static Dictionary<uint, string> DCWorlds = [];
    private static uint WorldToTravel;
    private static bool IsOnWorldTravelling;

    public override void Init()
    {
        ModuleConfig = LoadConfig<Config>() ?? new()
        {
            SelectedKinds =
            [
                ObjectKind.EventNpc, ObjectKind.EventObj, ObjectKind.Treasure, ObjectKind.Aetheryte,
                ObjectKind.GatheringPoint,
            ],
        };

        Service.Hook.InitializeFromAttributes(this);

        TargetSystem = FFXIVClientStructs.FFXIV.Client.Game.Control.TargetSystem.Instance();
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = false };

        ENpcTitles ??= LuminaCache.Get<ENpcResident>()
                                  .Where(x => x.Unknown10 && !string.IsNullOrWhiteSpace(x.Title.RawString))
                                  .ToDictionary(x => x.RowId, x => x.Title.RawString);

        ImportantENPC ??= LuminaCache.Get<ENpcResident>()
                                     .Where(x => x.Unknown10)
                                     .Select(x => x.RowId)
                                     .ToHashSet();

        AethernetShardName = LuminaCache.GetRow<EObjName>(2000151).Singular.RawString;

        Overlay ??= new Overlay(this, $"Daily Routines {Service.Lang.GetText("FastObjectInteractTitle")}");
        Overlay.Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.AlwaysAutoResize |
                        ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoCollapse;

        if (ModuleConfig.LockWindow) Overlay.Flags |= ImGuiWindowFlags.NoMove;
        else Overlay.Flags &= ~ImGuiWindowFlags.NoMove;

        Service.ClientState.TerritoryChanged += OnZoneChanged;
        Service.ClientState.Login += OnLogin;
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, "WorldTravelFinderReady", OnAddonWorldTravel);
        Service.FrameworkManager.Register(OnUpdate);

        OnZoneChanged(1);
        OnLogin();
    }

    private static void OnLogin()
    {
        var agent = AgentLobby.Instance();
        if (agent == null) return;

        var homeWorld = agent->LobbyData.HomeWorldId;
        if (homeWorld <= 0) return;

        var dataCenter = LuminaCache.GetRow<World>(homeWorld).DataCenter.Row;
        if (dataCenter <= 0) return;

        DCWorlds.Clear();
        DCWorlds = LuminaCache.Get<World>()
                              .Where(x => x.DataCenter.Row == dataCenter && !string.IsNullOrWhiteSpace(x.Name.RawString) &&
                                          !string.IsNullOrWhiteSpace(x.InternalName.RawString) &&
                                          IsChineseString(x.Name.RawString))
                              .ToDictionary(x => x.RowId, x => x.Name.RawString);
    }

    public override void ConfigUI()
    {
        ImGui.AlignTextToFramePadding();
        ImGui.Text($"{Service.Lang.GetText("FastObjectInteract-FontScale")}:");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(80f * ImGuiHelpers.GlobalScale);
        ImGui.InputFloat("###FontScaleInput", ref ModuleConfig.FontScale, 0f, 0f,
                         ModuleConfig.FontScale.ToString(CultureInfo.InvariantCulture));

        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            ModuleConfig.FontScale = Math.Max(0.1f, ModuleConfig.FontScale);
            SaveConfig(ModuleConfig);
        }

        ImGui.AlignTextToFramePadding();
        ImGui.Text($"{Service.Lang.GetText("FastObjectInteract-MinButtonWidth")}:");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(80f * ImGuiHelpers.GlobalScale);
        ImGui.InputFloat("###MinButtonWidthInput", ref ModuleConfig.MinButtonWidth, 0, 0,
                         ModuleConfig.MinButtonWidth.ToString(CultureInfo.InvariantCulture));

        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            ModuleConfig.MinButtonWidth = Math.Max(1, ModuleConfig.MinButtonWidth);
            SaveConfig(ModuleConfig);
        }

        ImGui.AlignTextToFramePadding();
        ImGui.Text($"{Service.Lang.GetText("FastObjectInteract-MaxDisplayAmount")}:");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(80f * ImGuiHelpers.GlobalScale);
        ImGui.InputInt("###MaxDisplayAmountInput", ref ModuleConfig.MaxDisplayAmount, 0, 0);
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            ModuleConfig.MaxDisplayAmount = Math.Max(1, ModuleConfig.MaxDisplayAmount);
            SaveConfig(ModuleConfig);
        }

        ImGui.AlignTextToFramePadding();
        ImGui.Text($"{Service.Lang.GetText("FastObjectInteract-SelectedObjectKinds")}:");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(300f * ImGuiHelpers.GlobalScale);
        if (ImGui.BeginCombo("###ObjectKindsSelection",
                             Service.Lang.GetText("FastObjectInteract-SelectedObjectKindsAmount", ModuleConfig.SelectedKinds.Count),
                             ImGuiComboFlags.HeightLarge))
        {
            foreach (var kind in Enum.GetValues<ObjectKind>())
            {
                if (!ObjectKindLoc.TryGetValue(kind, out var loc)) continue;

                var state = ModuleConfig.SelectedKinds.Contains(kind);
                if (ImGui.Checkbox(loc, ref state))
                {
                    if (!ModuleConfig.SelectedKinds.Remove(kind))
                        ModuleConfig.SelectedKinds.Add(kind);

                    SaveConfig(ModuleConfig);
                }
            }

            ImGui.EndCombo();
        }

        ImGui.AlignTextToFramePadding();
        ImGui.Text($"{Service.Lang.GetText("FastObjectInteract-BlacklistKeysList")}:");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(300f * ImGuiHelpers.GlobalScale);
        if (ImGui.BeginCombo("###BlacklistObjectsSelection",
                             Service.Lang.GetText("FastObjectInteract-BlacklistKeysListAmount",
                                                  ModuleConfig.BlacklistKeys.Count), ImGuiComboFlags.HeightLarge))
        {
            ImGui.SetNextItemWidth(250f * ImGuiHelpers.GlobalScale);
            ImGui.InputTextWithHint("###BlacklistKeyInput",
                                    $"{Service.Lang.GetText("FastObjectInteract-BlacklistKeysListInputHelp")}",
                                    ref BlacklistKeyInput, 100);

            ImGui.SameLine();
            if (ImGuiOm.ButtonIcon("###BlacklistKeyInputAdd", FontAwesomeIcon.Plus,
                                   Service.Lang.GetText("FastObjectInteract-Add")))
            {
                if (!ModuleConfig.BlacklistKeys.Add(BlacklistKeyInput)) return;

                SaveConfig(ModuleConfig);
            }

            ImGui.Separator();

            foreach (var key in ModuleConfig.BlacklistKeys)
            {
                if (ImGuiOm.ButtonIcon(key, FontAwesomeIcon.TrashAlt, Service.Lang.GetText("FastObjectInteract-Remove")))
                {
                    ModuleConfig.BlacklistKeys.Remove(key);
                    SaveConfig(ModuleConfig);
                }

                ImGui.SameLine();
                ImGui.Text(key);
            }

            ImGui.EndCombo();
        }

        if (ImGui.Checkbox(Service.Lang.GetText("FastObjectInteract-WindowInvisibleWhenInteract"),
                           ref ModuleConfig.WindowInvisibleWhenInteract))
            SaveConfig(ModuleConfig);

        if (ImGui.Checkbox(Service.Lang.GetText("FastObjectInteract-LockWindow"), ref ModuleConfig.LockWindow))
        {
            SaveConfig(ModuleConfig);

            if (ModuleConfig.LockWindow)
                Overlay.Flags |= ImGuiWindowFlags.NoMove;
            else
                Overlay.Flags &= ~ImGuiWindowFlags.NoMove;
        }

        if (ImGui.Checkbox(Service.Lang.GetText("FastObjectInteract-OnlyDisplayInViewRange"),
                           ref ModuleConfig.OnlyDisplayInViewRange))
            SaveConfig(ModuleConfig);

        if (ImGui.Checkbox(Service.Lang.GetText("FastObjectInteract-AllowClickToTarget"),
                           ref ModuleConfig.AllowClickToTarget))
            SaveConfig(ModuleConfig);
    }

    public override void OverlayUI()
    {
        PresetFont.Axis14.Push();

        ObjectToSelect? instanceChangeObject = null;
        ObjectToSelect? worldTravelObject = null;

        ImGui.BeginGroup();
        foreach (var objectToSelect in ObjectsToSelect.Values)
        {
            if (objectToSelect.GameObject == nint.Zero) continue;

            if (IsInInstancedArea && objectToSelect.Kind == ObjectKind.Aetheryte)
            {
                var gameObj = (GameObject*)objectToSelect.GameObject;
                if (Marshal.PtrToStringUTF8((nint)gameObj->Name) != AethernetShardName)
                    instanceChangeObject = objectToSelect;
            }

            if (!IsOnWorldTravelling && WorldTravelValidZones.Contains(Service.ClientState.TerritoryType) &&
                objectToSelect.Kind == ObjectKind.Aetheryte)
            {
                var gameObj = (GameObject*)objectToSelect.GameObject;
                if (Marshal.PtrToStringUTF8((nint)gameObj->Name) != AethernetShardName)
                    worldTravelObject = objectToSelect;
            }

            if (ModuleConfig.AllowClickToTarget)
            {
                if (objectToSelect.ButtonToTarget())
                    SaveConfig(ModuleConfig);
            }
            else
            {
                if (objectToSelect.ButtonNoTarget())
                    SaveConfig(ModuleConfig);
            }
        }

        ImGui.EndGroup();

        ImGui.SameLine();
        if (instanceChangeObject != null)
            InstanceZoneChangeWidget(instanceChangeObject);

        if (worldTravelObject != null)
            WorldChangeWidget(worldTravelObject);

        WindowWidth = Math.Max(ModuleConfig.MinButtonWidth, ImGui.GetItemRectSize().X);

        PresetFont.Axis14.Pop();
    }

    private void OnUpdate(IFramework framework)
    {
        if (!MonitorThrottler.Throttle("Monitor", 250)) return;

        var localPlayer = Service.ClientState.LocalPlayer;
        if (localPlayer == null)
        {
            ObjectsToSelect.Clear();
            WindowWidth = 0f;
            Overlay.IsOpen = false;
            return;
        }

        tempObjects.Clear();
        IsInInstancedArea = UIState.Instance()->AreaInstance.IsInstancedArea();
        IsOnWorldTravelling = localPlayer.OnlineStatus.Id == 25;

        foreach (var obj in Service.ObjectTable.ToArray())
        {
            if (!ObjectsThrottler.Throttle(obj.Address)) continue;

            if (!obj.IsTargetable || obj.IsDead) continue;

            var objName = obj.Name.TextValue;
            if (ModuleConfig.BlacklistKeys.Contains(objName)) continue;

            var objKind = obj.ObjectKind;
            if (!ModuleConfig.SelectedKinds.Contains(objKind)) continue;

            var dataID = obj.DataId;
            if (objKind == ObjectKind.EventNpc && !ImportantENPC.Contains(dataID))
            {
                if (!ImportantENPC.Contains(dataID)) continue;
                if (ENpcTitles.TryGetValue(dataID, out var ENPCTitle))
                    objName = string.Format(ENPCTiltleText, ENPCTitle, obj.Name);
            }

            var gameObj = (GameObject*)obj.Address;
            if (ModuleConfig.OnlyDisplayInViewRange)
            {
                if (!TargetSystem->IsObjectInViewRange(gameObj))
                    continue;
            }

            var objDistance = Vector3.Distance(localPlayer.Position, obj.Position);
            if (objDistance > 20 || localPlayer.Position.Y - gameObj->Position.Y > 4) continue;

            if (tempObjects.Count > ModuleConfig.MaxDisplayAmount) break;
            tempObjects.Add(new ObjectToSelect((nint)gameObj, objName, objKind, objDistance));
        }

        tempObjects.Sort((a, b) => a.Distance.CompareTo(b.Distance));

        ObjectsToSelect.Clear();
        foreach (var tempObj in tempObjects) ObjectsToSelect.Add(tempObj.GameObject, tempObj);

        if (Overlay == null) return;
        if (!IsWindowShouldBeOpen())
        {
            Overlay.IsOpen = false;
            WindowWidth = 0f;
        }
        else
            Overlay.IsOpen = true;
    }

    private static void OnZoneChanged(ushort zone)
    {
        WorldToTravel = 0;
        AgentWorldTravelReceiveEventHook.Disable();

        if (zone == 0 || zone == Service.ClientState.TerritoryType) return;

        InstancedAreaAmount = 3;
    }

    private static void OnAddonWorldTravel(AddonEvent type, AddonArgs args)
    {
        if (WorldToTravel == 0) return;

        var addon = (AtkUnitBase*)args.Addon;
        if (addon == null) return;

        if (TryGetAddonByName<AtkUnitBase>("SelectYesno", out var addon0) && IsAddonAndNodesReady(addon0))
            addon0->Close(true);

        addon->GetTextNodeById(31)->SetText(LuminaCache.GetRow<World>(WorldToTravel).Name.RawString);
    }

    private static nint AgentWorldTravelReceiveEventDetour(
        AgentWorldTravel* agent, nint a2, nint a3, nint a4, long eventCase)
    {
        if (WorldToTravel == 0)
        {
            AgentWorldTravelReceiveEventHook.Disable();
            return AgentWorldTravelReceiveEventHook.Original(agent, a2, a3, a4, eventCase);
        }

        agent->WorldToTravel = WorldToTravel;
        return AgentWorldTravelReceiveEventHook.Original(agent, a2, a3, a4, eventCase);
    }

    private void InstanceZoneChangeWidget(ObjectToSelect objectToSelect)
    {
        var gameObject = (GameObject*)objectToSelect.GameObject;
        var instance = UIState.Instance()->AreaInstance;

        ImGui.BeginGroup();
        for (var i = 1; i < InstancedAreaAmount + 1; i++)
        {
            if (i == instance.Instance) continue;

            ImGui.BeginDisabled(!objectToSelect.IsReacheable());
            if (ButtonCenterText($"InstanceChangeWidget_{i}",
                                 Service.Lang.GetText("FastObjectInteract-InstanceAreaChange", i)))
                ChangeInstanceZone(gameObject, i);

            ImGui.EndDisabled();
        }

        ImGui.EndGroup();

        return;

        void ChangeInstanceZone(GameObject* obj, int zone)
        {
            TaskManager.Abort();

            TaskManager.Enqueue(() => InteractWithObject(obj, ObjectKind.Aetheryte));

            TaskManager.Enqueue(() =>
            {
                if (!TryGetAddonByName<AtkUnitBase>("SelectString", out var addon) ||
                    !IsAddonAndNodesReady(addon)) return false;

                return ClickHelper.SelectString("切换副本区");
            });

            TaskManager.Enqueue(() =>
            {
                if (!TryGetAddonByName<AtkUnitBase>("SelectString", out var addon) ||
                    !IsAddonAndNodesReady(addon)) return false;

                if (!MemoryHelper.ReadSeStringNullTerminated((nint)addon->AtkValues[2].String).TextValue
                                 .Contains("为了缓解服务器压力")) return false;

                InstancedAreaAmount = ((AddonSelectString*)addon)->PopupMenu.PopupMenu.EntryCount - 2;
                return Click.TrySendClick($"select_string{zone + 1}");
            });
        }
    }

    private void WorldChangeWidget(ObjectToSelect _)
    {
        var lobbyData = AgentLobby.Instance()->LobbyData;
        ImGui.BeginGroup();
        foreach (var worldPair in DCWorlds)
        {
            if (worldPair.Key == lobbyData.CurrentWorldId) continue;

            if (ButtonCenterText($"WorldTravelWidget_{worldPair.Key}", worldPair.Value))
                WorldTravel(worldPair.Key);
        }

        ImGui.EndGroup();
        return;

        void WorldTravel(uint worldID)
        {
            TaskManager.Abort();

            TaskManager.Enqueue(() => Service.ExecuteCommandManager.ExecuteCommand(ExecuteCommandFlag.RequestWorldTravel));
            TaskManager.Enqueue(() => WorldToTravel = worldID);
            TaskManager.Enqueue(AgentWorldTravelReceiveEventHook.Enable);

            TaskManager.Enqueue(() =>
            {
                if (!EzThrottler.Throttle("FastObjectInteract-WorldTravelAgentShow", 100)) return false;

                AgentWorldTravel.Instance()->AgentInterface.Show();
                return AgentWorldTravel.Instance()->AgentInterface.IsAgentActive();
            });

            TaskManager.Enqueue(() =>
            {
                if (!EzThrottler.Throttle("FastObjectInteract-WorldTravelSelectWorld", 100)) return false;

                var addon = (AtkUnitBase*)Service.Gui.GetAddonByName("WorldTravelSelect");

                AddonHelper.Callback(addon, true, 2);
                return TryGetAddonByName<AtkUnitBase>("SelectYesno", out var _);
            });

            TaskManager.Enqueue(() =>
            {
                if (!TryGetAddonByName<AtkUnitBase>("SelectYesno", out var addon) || !IsAddonAndNodesReady(addon))
                    return false;

                ClickSelectYesNo.Using((nint)addon).Yes();
                addon->Close(true);
                return true;
            });

            TaskManager.Enqueue(AgentWorldTravelReceiveEventHook.Disable);
        }
    }

    private static void InteractWithObject(GameObject* obj, ObjectKind kind)
    {
        FFXIVClientStructs.FFXIV.Client.Game.Control.TargetSystem.Instance()->Target = obj;
        FFXIVClientStructs.FFXIV.Client.Game.Control.TargetSystem.Instance()->InteractWithObject(obj);
        if (kind is ObjectKind.EventObj)
            FFXIVClientStructs.FFXIV.Client.Game.Control.TargetSystem.Instance()->OpenObjectInteraction(obj);
    }

    private static bool IsWindowShouldBeOpen()
        => ObjectsToSelect.Count != 0 && (!ModuleConfig.WindowInvisibleWhenInteract || !IsOccupied());

    public static bool ButtonCenterText(string id, string text)
    {
        ImGui.PushID(id);
        ImGui.SetWindowFontScale(ModuleConfig.FontScale);

        var textSize = ImGui.CalcTextSize(text);

        var cursorPos = ImGui.GetCursorScreenPos();
        var padding = ImGui.GetStyle().FramePadding;
        var buttonWidth = Math.Max(WindowWidth, textSize.X + (padding.X * 2));
        var result = ImGui.Button(string.Empty, new Vector2(buttonWidth, textSize.Y + (padding.Y * 2)));

        ImGui.GetWindowDrawList()
             .AddText(new Vector2(cursorPos.X + ((buttonWidth - textSize.X) / 2), cursorPos.Y + padding.Y),
                      ImGui.GetColorU32(ImGuiCol.Text), text);

        ImGui.SetWindowFontScale(1);
        ImGui.PopID();

        return result;
    }

    public override void Uninit()
    {
        base.Uninit();

        Service.ClientState.Login -= OnLogin;
        Service.ClientState.TerritoryChanged -= OnZoneChanged;
        Service.AddonLifecycle.UnregisterListener(OnAddonWorldTravel);
        ObjectsToSelect.Clear();
    }


    [GeneratedRegex("\\[.*?\\]")]
    private static partial Regex AddToBlacklistNameRegex();

    [GeneratedRegex("\\[.*?\\]")]
    private static partial Regex FastObjectInteractTitleRegex();

    [StructLayout(LayoutKind.Explicit)]
    private struct AgentWorldTravel
    {
        [FieldOffset(0)]
        public AgentInterface AgentInterface;

        [FieldOffset(76)]
        public uint WorldToTravel;

        public static AgentWorldTravel* Instance() =>
            (AgentWorldTravel*)AgentModule.Instance()->GetAgentByInternalId(AgentId.WorldTravel);
    }

    private sealed record ObjectToSelect(nint GameObject, string Name, ObjectKind Kind, float Distance)
    {
        public bool ButtonToTarget()
        {
            var colors = ImGui.GetStyle().Colors;

            if (!IsReacheable())
            {
                ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.5f);
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, colors[(int)ImGuiCol.HeaderActive]);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, colors[(int)ImGuiCol.HeaderHovered]);
            }

            ButtonCenterText(GameObject.ToString(), Name);

            if (!IsReacheable())
            {
                ImGui.PopStyleColor(2);
                ImGui.PopStyleVar();
            }

            if (ImGui.IsItemClicked(ImGuiMouseButton.Left) && IsReacheable())
                InteractWithObject((GameObject*)GameObject, Kind);
            else if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                FFXIVClientStructs.FFXIV.Client.Game.Control.TargetSystem.Instance()->Target = (GameObject*)GameObject;

            return AddToBlacklist();
        }

        public bool ButtonNoTarget()
        {
            ImGui.BeginDisabled(!IsReacheable());
            if (ButtonCenterText(GameObject.ToString(), Name))
                InteractWithObject((GameObject*)GameObject, Kind);

            ImGui.EndDisabled();

            return AddToBlacklist();
        }

        private bool AddToBlacklist()
        {
            var state = false;
            if (ImGui.BeginPopupContextItem($"{GameObject}_{Name}"))
            {
                if (ImGui.MenuItem(Service.Lang.GetText("FastObjectInteract-AddToBlacklist")))
                {
                    if (ModuleConfig.BlacklistKeys.Add(FastObjectInteractTitleRegex().Replace(Name, "").Trim()))
                        state = true;
                }

                ImGui.EndPopup();
            }

            return state;
        }

        public bool IsReacheable() =>
            Kind switch
            {
                ObjectKind.EventObj => Distance < 4.7999999,
                ObjectKind.EventNpc => Distance < 6.9999999,
                ObjectKind.Aetheryte => Distance < 11.0,
                ObjectKind.GatheringPoint => Distance < 3.0,
                _ => Distance < 6.0,
            };

        public bool Equals(ObjectToSelect? other)
        {
            if (other is null || GetType() != other.GetType())
                return false;

            return GameObject == other.GameObject;
        }

        public override int GetHashCode() { return HashCode.Combine(GameObject); }
    }

    private class Config : ModuleConfiguration
    {
        public HashSet<string> BlacklistKeys = [];
        public HashSet<ObjectKind> SelectedKinds = [];

        public bool AllowClickToTarget;
        public float FontScale = 1f;
        public bool LockWindow;
        public int MaxDisplayAmount = 5;
        public float MinButtonWidth = 300f;
        public bool OnlyDisplayInViewRange;
        public bool WindowInvisibleWhenInteract = true;
    }
}
