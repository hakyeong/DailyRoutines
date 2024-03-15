using System;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using ClickLib;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Hooking;
using Dalamud.Memory;
using Dalamud.Utility;
using Dalamud.Utility.Signatures;
using ECommons.Automation;
using ECommons.GameFunctions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using Character = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;
using Map = Lumina.Excel.GeneratedSheets.Map;

namespace DailyRoutines.Modules;

[ModuleDescription("DevModuleTitle", "DevModuleDescription", ModuleCategories.Base)]
public unsafe class DevModuleBase : DailyModuleBase
{
    [StructLayout(LayoutKind.Explicit, Size = 0xF8)]
    public struct AtkComponentRadioButton
    {
        [FieldOffset(0)]
        public AtkComponentButton AtkComponentButton;

        public readonly bool IsSelected => (AtkComponentButton.Flags & 0x40000) != 0;
    }

    [Flags]
    public enum VisibilityFlags
    {
        None = 0,
        Unknown0 = 1 << 0,
        Model = 1 << 1,
        Unknown2 = 1 << 2,
        Unknown3 = 1 << 3,
        Unknown4 = 1 << 4,
        Unknown5 = 1 << 5,
        Unknown6 = 1 << 6,
        Unknown7 = 1 << 7,
        Unknown8 = 1 << 8,
        Unknown9 = 1 << 9,
        Unknown10 = 1 << 10,
        Nameplate = 1 << 11,
        Unknown12 = 1 << 12,
        Unknown13 = 1 << 13,
        Unknown14 = 1 << 14,
        Unknown15 = 1 << 15,
        Unknown16 = 1 << 16,
        Unknown17 = 1 << 17,
        Unknown18 = 1 << 18,
        Unknown19 = 1 << 19,
        Unknown20 = 1 << 20,
        Unknown21 = 1 << 21,
        Unknown22 = 1 << 22,
        Unknown23 = 1 << 23,
        Unknown24 = 1 << 24,
        Unknown25 = 1 << 25,
        Unknown26 = 1 << 26,
        Unknown27 = 1 << 27,
        Unknown28 = 1 << 28,
        Unknown39 = 1 << 29,
        Unknown30 = 1 << 30,
        Unknown31 = 1 << 31,
        Invisible = Model | Nameplate
    }

    private delegate nint IsFlightProhibitedDelegate(nint a1);

    [Signature("48 89 5C 24 ?? 57 48 83 EC 20 48 8B 1D ?? ?? ?? ?? 48 8B F9 48 85 DB 0F 84 ?? ?? ?? ?? 80 3D",
               DetourName = nameof(IsFlightProhibited))]
    private Hook<IsFlightProhibitedDelegate>? IsFlightProhibitedHook;

    internal delegate nint SetupInstanceContentDelegate(nint a1, uint a2, uint a3, uint a4);

    [Signature("48 89 5C 24 ?? 57 48 83 EC 20 48 8B F9 48 81 C1 ?? ?? ?? ?? E8 ?? ?? ?? ?? E8",
               DetourName = nameof(SetupInstanceContent))]
    internal Hook<SetupInstanceContentDelegate>? SetupInstanceContentHook;

    internal delegate nint LoadZoneDelegate(nint a1, uint a2, int a3, byte a4, byte a5, byte a6);

    [Signature("40 55 56 57 41 56 41 57 48 83 EC 50 48 8B F9", DetourName = nameof(LoadZone))]
    internal Hook<LoadZoneDelegate>? LoadZoneHook;

    internal delegate nint SetupTerritoryTypeDelegate(void* EventFramework, ushort territoryType);

    internal SetupTerritoryTypeDelegate? SetupTerritoryType;

    private static float SpecifiedY;
    private static int ZoneId;
    private static bool IsFlightEnabled;
    private static bool IsFlightAllowed;
    private static string SearchObjectInput = string.Empty;

    private delegate nint AddonReceiveEventDelegate(
        AtkEventListener* self, AtkEventType eventType, uint eventParam, AtkEvent* eventData, ulong* inputData);

    private delegate nint AgentReceiveEventDelegate(
        AgentInterface* agent, nint rawData, AtkValue* args, uint argCount, ulong sender);

    private Hook<AddonReceiveEventDelegate>? AddonTestHook;
    private Hook<AgentReceiveEventDelegate>? AgentTestHook;

    private delegate long TestDelegate(uint a1, long a2);

    [Signature("48 89 5C 24 ?? 57 48 83 EC ?? 48 8B DA 8B F9 0F B6 52", DetourName = nameof(Test))]
    private Hook<TestDelegate>? TestHook;

    public override void Init()
    {
        Service.Hook.InitializeFromAttributes(this);
        SetupInstanceContentHook?.Enable();
        TestHook?.Enable();
        LoadZoneHook?.Enable();
    }

    public override void ConfigUI()
    {
        if (ImGui.BeginTabBar("DevModuleTab"))
        {
            if (ImGui.BeginTabItem("信息相关"))
            {
                if (ImGui.Button("输出点击名"))
                {
                    foreach (var clickName in Click.GetClickNames())
                        Service.Log.Debug(clickName);
                }

                if (ImGui.Button("输出 Toast"))
                {
                    if (HelpersOm.IsGameForeground())
                    {
                        Service.Notice.Notify(
                            "", Service.Lang.GetText("AutoNotifyCutSceneEnd-NotificationMessage"));
                    }
                }

                if (ImGui.Button("获取当前状态"))
                {
                    var statuses = Service.ClientState.LocalPlayer.StatusList;
                    foreach (var status in statuses) Service.Log.Debug($"{status.GameData.Name} {status.StatusId}");
                }

                if (ImGui.Button("输出聊天信息"))
                {
                    Service.Chat.Print(Service.Lang.GetSeString("AutoSubmarineCollect-LackCeruleumTanks", SeString.CreateItemLink(
                                                                    10155)));
                }

                ImGui.SameLine();
                if (ImGui.Button("读取LB信息"))
                {
                    var controller = UIState.Instance()->LimitBreakController;
                    Service.Log.Debug($"{controller.BarCount} | {controller.BarValue:X} | {controller.CurrentValue}");
                }

                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("本地传送相关"))
            {
                if (ImGui.Button("传送到FLAG地图"))
                {
                    var territoryId = AgentMap.Instance()->FlagMapMarker.TerritoryId;
                    if (Service.ClientState.TerritoryType != territoryId)
                    {
                        var aetheryte = territoryId == 399
                                            ? Service.Data.GetExcelSheet<Map>().GetRow(territoryId)
                                                     ?.TerritoryType?.Value?.Aetheryte.Value
                                            : Service.Data.GetExcelSheet<Aetheryte>()
                                                     .FirstOrDefault(
                                                         x => x.IsAetheryte && x.Territory.Row == territoryId);

                        if (aetheryte != null) Telepo.Instance()->Teleport(aetheryte.RowId, 0);
                    }
                }

                if (ImGui.Button("传送到FLAG"))
                {
                    var targetPos = new Vector3(AgentMap.Instance()->FlagMapMarker.XFloat, SpecifiedY,
                                                AgentMap.Instance()->FlagMapMarker.YFloat);
                    Teleport(targetPos);
                }

                ImGui.SameLine();
                ImGui.SetNextItemWidth(150f);
                ImGui.InputFloat("指定Y", ref SpecifiedY);
                ImGui.SameLine();
                ImGui.Text($"当前 Y :{Service.ClientState.LocalPlayer?.Position.Y}");

                ImGui.BeginGroup();
                if (ImGui.Button("X + 5"))
                {
                    var currentPos = Service.ClientState.LocalPlayer.Position;
                    Teleport(currentPos with { X = currentPos.X + 5 });
                }

                if (ImGui.Button("X - 5"))
                {
                    var currentPos = Service.ClientState.LocalPlayer.Position;
                    Teleport(currentPos with { X = currentPos.X - 5 });
                }

                ImGui.EndGroup();

                ImGui.SameLine();
                ImGui.BeginGroup();
                if (ImGui.Button("Y + 5"))
                {
                    var currentPos = Service.ClientState.LocalPlayer.Position;
                    Teleport(currentPos with { Y = currentPos.Y + 5 });
                }

                if (ImGui.Button("Y - 5"))
                {
                    var currentPos = Service.ClientState.LocalPlayer.Position;
                    Teleport(currentPos with { Y = currentPos.Y - 5 });
                }

                ImGui.EndGroup();

                ImGui.SameLine();
                ImGui.BeginGroup();
                if (ImGui.Button("Z + 5"))
                {
                    var currentPos = Service.ClientState.LocalPlayer.Position;
                    Teleport(currentPos with { Z = currentPos.Z + 5 });
                }

                if (ImGui.Button("Z - 5"))
                {
                    var currentPos = Service.ClientState.LocalPlayer.Position;
                    Teleport(currentPos with { Z = currentPos.Z - 5 });
                }

                ImGui.EndGroup();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("游戏功能相关"))
            {
                if (ImGui.Button("测试进入")) EnterOldMap();

                ImGui.SameLine();
                ImGui.SetNextItemWidth(150f);
                ImGui.InputInt("地图ID", ref ZoneId);

                if (ImGui.Button("测试获取地点"))
                {
                    var agent = AgentModule.Instance()->GetAgentMap();
                    if (agent == null) return;

                    foreach (var marker in agent->MapMarkerInfoArraySpan)
                    {
                        Service.Log.Debug($"{MemoryHelper.ReadStringNullTerminated((nint)marker.MapMarker.Subtext)} {marker.MapMarker.X} {marker.MapMarker.Y} {marker.MapMarker.Index} {marker.DataKey} {marker.DataType}");
                    }

                }

                if (ImGui.Button("测试 Hook Addon"))
                {
                    var addon = (AddonGrandCompanySupplyList*)Service.Gui.GetAddonByName("GrandCompanySupplyList");
                    var address = (nint)addon->ExpertDeliveryList->AtkComponentBase.AtkEventListener.vfunc[2];
                    AddonTestHook ??=
                        Service.Hook.HookFromAddress<AddonReceiveEventDelegate>(address, AddonReceiveEventDetour);
                    AddonTestHook?.Enable();
                }

                if (ImGui.Button("找寻豆芽"))
                {
                    foreach (var player in Service.ObjectTable)
                    {
                        if (player.ObjectKind != ObjectKind.Player) continue;
                        var playerBC = (Character*)player.Address;
                        if (!string.IsNullOrEmpty(
                                MemoryHelper.ReadStringNullTerminated((nint)playerBC->FreeCompanyTag))) continue;
                        if (playerBC->CharacterData.OnlineStatus == 32)
                        {
                            Service.Chat.Print(
                                $"发现 {player.Name} 于 {MapUtil.WorldToMap(new Vector2(player.Position.X, player.Position.Z))} 等级: {playerBC->CharacterData.Level} 职业: {playerBC->CharacterData.ClassJob}");
                        }
                    }
                }

                ImGui.SameLine();
                ImGui.SetNextItemWidth(150f);
                ImGui.InputText("名称", ref SearchObjectInput, 100);

                ImGui.SameLine();
                if (ImGui.Button("找寻目标"))
                {
                    if (SearchObjectInput.IsNullOrWhitespace()) return;
                    foreach (var obj in Service.ObjectTable)
                    {
                        if (obj.IsDead || !obj.IsValid()) continue;
                        if (obj.Name.ExtractText() == SearchObjectInput)
                        {
                            var gamePos = MapUtil.WorldToMap(new Vector2(obj.Position.X, obj.Position.Z));
                            var payload = new MapLinkPayload(Service.ClientState.TerritoryType,
                                                             Service.Data
                                                                    .GetExcelSheet<TerritoryType>()
                                                                    .GetRow(Service.ClientState
                                                                                .TerritoryType).Map.RawRow
                                                                    .RowId, gamePos.X,
                                                             gamePos.Y);
                            var message = new SeStringBuilder().Add(new TextPayload($"发现目标位于: {gamePos}")).Add(payload)
                                                               .BuiltString;
                            Service.Chat.Print(message);
                            Service.Gui.OpenMapWithMapLink(payload);
                        }
                    }
                }

                if (ImGui.Button("写入TLB"))
                {
                    var manager = Service.ClientState.LocalPlayer.BattleChara()->GetStatusManager;
                    manager->AddStatus(158);
                    /*var newStatus = new Status
                    {
                        Param = 0,
                        RemainingTime = 9999,
                        SourceID = 3758096384u,
                        StackCount = 0,
                        StatusID = 1931
                    };

                    var size = Marshal.SizeOf(newStatus);
                    var buffer = Marshal.AllocHGlobal(size);
                    try
                    {
                        Marshal.StructureToPtr(newStatus, buffer, false);

                        var targetAddress = (IntPtr)((long)manager + 8 + (3 * size));

                        var bytes = new byte[size];
                        Marshal.Copy(buffer, bytes, 0, size);
                        Marshal.Copy(bytes, 0, targetAddress, size);
                    } finally
                    {
                        Marshal.FreeHGlobal(buffer);
                    }*/
                }

                ImGui.SameLine();
                if (ImGui.Button("移除TLB"))
                {
                    var manager = Service.ClientState.LocalPlayer.BattleChara()->GetStatusManager;
                    var idx = manager->GetStatusIndex(158);
                    if (idx != -1)
                        manager->RemoveStatus(idx);
                }

                if (ImGui.Checkbox("启用飞行功能", ref IsFlightEnabled))
                {
                    if (IsFlightEnabled)
                        IsFlightProhibitedHook?.Enable();
                    else
                        IsFlightProhibitedHook?.Disable();
                }

                if (IsFlightEnabled)
                    ImGui.Checkbox("启用飞行", ref IsFlightAllowed);

                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    private long Test(uint objectID, long a2)
    {
        //Service.Log.Debug($"{objectID} {a2}");
        return TestHook.Original(objectID, a2);
    }

    private nint AddonReceiveEventDetour(
        AtkEventListener* self, AtkEventType eventType, uint eventParam, AtkEvent* eventData, ulong* inputData)
    {
        Service.Log.Debug(
            $"RCV: listener= {(nint)self:X}, type={eventType}, param={eventParam}, input={inputData[0]:X16} {inputData[1]:X16} {inputData[2]:X16}");

        return AddonTestHook.Original(self, eventType, eventParam, eventData, inputData);
    }

    private nint AgentReceiveEventDetour(
        AgentInterface* agent, nint rawData, AtkValue* args, uint argCount, ulong sender)
    {
        Service.Log.Debug(
            $"RCV: agent= {(nint)agent:X}, rawData={rawData:X}, param={args[0]}, argCount={argCount} sender = {sender}");

        return AgentTestHook.Original(agent, rawData, args, argCount, sender);
    }

    private nint SetupInstanceContent(nint a1, uint a2, uint a3, uint a4)
    {
        if (a3 == 86)
        {
            // TestHook2.Original(100000000000);
            // a3 = 1;
            // a2 = 0x80030000 + a3;
        }

        return SetupInstanceContentHook.Original(a1, a2, a3, a4);
    }

    private nint LoadZone(nint a1, uint a2, int a3, byte a4, byte a5, byte a6)
    {
        // if (a2 == 1044)
        // {
        //     a2 = Service.ClientState.TerritoryType;
        // }
        return LoadZoneHook.Original(a1, a2, a3, a4, a5, a6);
    }

    private void EnterOldMap()
    {
        foreach (var obj in Service.ObjectTable) obj.Struct()->DisableDraw();
        //SetupInstanceContentHook.Original((nint)EventFramework.Instance(), 0x80030000 + 0, 0, 0);
        LoadZone((nint)GameMain.Instance(), (uint)ZoneId, 0, 0, 1, 0);
        // SetupTerritoryType(EventFramework.Instance(), (ushort)1039);
    }

    private static bool? Teleport(Vector3 pos)
    {
        if (IsOccupied()) return false;

        if (Service.ClientState.LocalPlayer != null)
        {
            var address = Service.ClientState.LocalPlayer.Address;
            MemoryHelper.Write(address + 176, pos.X);
            MemoryHelper.Write(address + 180, pos.Y);
            MemoryHelper.Write(address + 184, pos.Z);

            if (Service.Condition[ConditionFlag.Mounted])
            {
                var mount = Service.ObjectTable
                                   .First(obj => obj.ObjectKind == ObjectKind.MountType).Address;
                MemoryHelper.Write(mount + 176, pos.X);
                MemoryHelper.Write(mount + 180, pos.Y);
                MemoryHelper.Write(mount + 184, pos.Z);
            }

            return true;
        }

        return false;
    }

    private static nint IsFlightProhibited(nint a1)
    {
        return IsFlightEnabled && IsFlightAllowed ? 0 : a1;
    }
}
