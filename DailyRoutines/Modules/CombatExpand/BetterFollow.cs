using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using DailyRoutines.Infos;
using DailyRoutines.IPC;
using DailyRoutines.Managers;
using Dalamud;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;
using System.Numerics;
using System.Windows.Forms;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using GameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace DailyRoutines.Modules;

[StructLayout(LayoutKind.Explicit, Size = 0x1BD0)]
internal unsafe partial struct CharacterFlying
{
    [FieldOffset(0x60C)]
    public byte IsFlying;
}

[ModuleDescription("BetterFollowTitle", "BetterFollowDescription", ModuleCategories.CombatExpand)]
public unsafe class BetterFollow : DailyModuleBase
{
    public override string? Author { get; set; } = "HSS";

    // 状态
    private static bool _FollowStatus;
    private static bool _enableReFollow;
    private static string _LastFollowObjectName = "无";
    private static bool _LastFollowObjectStatus;

    // 数据
    private static nint _LastFollowObjectAddress;
    private static uint _LastFollowObjectId;
    private static nint _a1;
    private static nint _a1_data;
    private static nint _v5;
    private static nint _d1;
    private static nint _v8;

    // Hook
    private delegate void FollowDataDelegate(ulong a1, nint a2);

    [Signature("E8 ?? ?? ?? ?? EB ?? 48 81 C1 ?? ?? ?? ?? E8 ?? ?? ?? ?? EB", DetourName = nameof(FollowData))]
    private readonly Hook<FollowDataDelegate>? FollowDataHook;

    [Signature(
        "40 53 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 48 8B D9 48 8D 4C 24 ?? E8 ?? ?? ?? ?? 48 8B D0 48 8D 8B ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8D 4C 24 ?? E8 ?? ?? ?? ?? 48 8B 4C 24 ?? BA ?? ?? ?? ?? 41 B8 ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B 8C 24 ?? ?? ?? ?? 48 33 CC E8 ?? ?? ?? ?? 48 81 C4 ?? ?? ?? ?? 5B C3 CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC 48 89 5C 24")]
    private readonly delegate* unmanaged<nint, uint, nint, nint, ulong> FollowStart;

    [Signature("48 89 5C 24 ?? 57 48 83 EC ?? 48 8B F9 48 8B DA 48 8B CA E8 ?? ?? ?? ?? 44 8B 00")]
    private readonly delegate* unmanaged<nint, nint, ulong> FollowDataPush;

    // 配置
    private static bool AutoReFollow = true;
    private static bool OnCombatOver;
    private static bool OnDuty;
    private static bool ForcedFollow;
    private static float Delay = 0.5f;
    private static bool MountWhenFollowMount;
    private static bool FlyingWhenFollowFlying;

    private enum MoveTypeList
    {
        System,
        Nvavmesh
    }

    private static MoveTypeList MoveType = MoveTypeList.System;

    private static readonly Dictionary<MoveTypeList, string> MoveTypeLoc = new()
    {
        { MoveTypeList.System, Service.Lang.GetText("BetterFollow-SystemFollow") },
        { MoveTypeList.Nvavmesh, Service.Lang.GetText("BetterFollow-NvavmeshFollow") },
    };

    private const string CommandStr = "/pdrfollow";

    private static vnavmeshIPC? vnavmesh;

    public override void Init()
    {
        #region Config
        AddConfig("AutoReFollow", AutoReFollow);
        AutoReFollow = GetConfig<bool>("AutoReFollow");

        AddConfig("OnCombatOver", OnCombatOver);
        OnCombatOver = GetConfig<bool>("OnCombatOver");

        AddConfig("Delay", Delay);
        Delay = GetConfig<float>("Delay");

        AddConfig("OnDuty", OnDuty);
        OnDuty = GetConfig<bool>("OnDuty");

        AddConfig("ForcedFollow", ForcedFollow);
        ForcedFollow = GetConfig<bool>("ForcedFollow");

        AddConfig("MountWhenFollowMount", true);
        MountWhenFollowMount = GetConfig<bool>("MountWhenFollowMount");

        AddConfig("FlyingWhenFollowFlying", true);
        FlyingWhenFollowFlying = GetConfig<bool>("FlyingWhenFollowFlying");

        AddConfig("MoveType", true);
        MoveType = GetConfig<MoveTypeList>("MoveType");

        #endregion

        _a1_data = 0;
        _v5 = 0;
        _d1 = 0;
        _v8 = 0;
        _FollowStatus = false;
        _enableReFollow = false;
        _LastFollowObjectAddress = 0;
        _LastFollowObjectId = 0;

        //&unk_1421CF590
        _a1 = Service.SigScanner.GetStaticAddressFromSig(
            "48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? EB ?? 48 8D 15 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 84 DB");
        //&unk_1421CF9C0
        //_a2 = _BassAddress + 0x21CF9C0;
        //qword_1421CFB60
        _v5 = *(nint*)Service.SigScanner.GetStaticAddressFromSig("4C 8B 35 ?? ?? ?? ?? 48 3B D0");
        //sub_141351D70(a1 + 1104, v6-objAddress);
        _a1_data = _a1 + 0x450;
        //*(_BYTE *)(a1 + 1369)
        _d1 = _a1 + 1369;
        //141A13598+58->sub_140629380->v7 + 6600 g_Client::System::Framework::Framework_InstancePointer2
        _v8 = *(nint*)((*(ulong*)Service.SigScanner.GetStaticAddressFromSig("48 8B 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B C8 48 8B 10 FF 52 ?? 4C 8B CE")) + 0x2B60)+0x19c8;
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = false };
        _FollowStatus = *(int*)_d1 == 4;
        Service.Hook.InitializeFromAttributes(this);
        FollowDataHook?.Enable();

        vnavmesh ??= Service.IPCManager.Load<vnavmeshIPC>(this);
        if (vnavmesh == null)
        {
            Service.Log.Warning("当前未安装或启用 vnavmesh, 相关功能已禁用");
        }

        Service.Framework.Update += ReFollowOnFramework;
        Service.Framework.Update += FollowOnFramework;
        Service.Chat.ChatMessage += OnChatMessage;
        if (ForcedFollow)
            CommandManager.AddCommand(CommandStr,
                                      new CommandInfo(OnCommand)
                                      {
                                          HelpMessage = Service.Lang.GetText("BetterFollow-CommandDesc", CommandStr)
                                      });
    }

    public override void ConfigUI()
    {
        ConflictKeyText();

        ImGui.Spacing();
        
        if (_FollowStatus)
        {
            ImGui.BeginDisabled();
        }
        ImGui.Text(Service.Lang.GetText("BetterFollow-MoveType"));
        ImGui.SetNextItemWidth(300f);
        if (ImGui.BeginCombo("###MoveTypoCombo", MoveTypeLoc[MoveType]))
        {
            foreach (var mode in MoveTypeLoc)
            {
                if (ImGui.Selectable(mode.Value, mode.Key == MoveType))
                {
                    MoveType = mode.Key;
                    UpdateConfig("MoveType", MoveType);
                }
            }

            ImGui.EndCombo();
        }
        if (_FollowStatus)
        {
            ImGui.EndDisabled();
        }

        if (MoveType == MoveTypeList.Nvavmesh)
        {
            ImGui.SameLine();
            ImGui.Text(Service.Lang.GetText("BetterFollow-NvavmeshFollowDesc"));
        }
        
        ImGui.SetNextItemWidth(300f);
        ImGui.SliderFloat(Service.Lang.GetText("BetterFollow-DelayConfig"), ref Delay, 0.5f, 5f, "%.1f");
        if (ImGui.IsItemDeactivatedAfterEdit())
            UpdateConfig("Delay", Delay);

        if (ImGui.Checkbox(Service.Lang.GetText("AutoMount-MountWhenFollowMount"), ref MountWhenFollowMount))
            UpdateConfig("MountWhenFollowMount", MountWhenFollowMount);

        if (MountWhenFollowMount)
        {
            ImGui.Indent();
            if (ImGui.Checkbox(Service.Lang.GetText("AutoMount-FlyingWhenFollowFlying"), ref FlyingWhenFollowFlying))
                UpdateConfig("FlyingWhenFollowFlying", FlyingWhenFollowFlying);
            ImGui.Unindent();
        }
        
        if (ImGui.Checkbox(Service.Lang.GetText("BetterFollow-AutoReFollowConfig"), ref AutoReFollow))
            UpdateConfig("AutoReFollow", AutoReFollow);
        if (AutoReFollow)
        {
            ImGui.Indent();
            ImGui.Text(Service.Lang.GetText("BetterFollow-Status", _enableReFollow, _LastFollowObjectName,
                                            _LastFollowObjectStatus));

            if (ImGui.Checkbox(Service.Lang.GetText("BetterFollow-OnCombatOverConfig"), ref OnCombatOver))
                UpdateConfig("OnCombatOver", OnCombatOver);

            if (ImGui.Checkbox(Service.Lang.GetText("BetterFollow-OnDutyConfig"), ref OnDuty))
                UpdateConfig("OnCombatOver", OnDuty);

            

            ImGui.Unindent();
        }

        if (ImGui.Checkbox(Service.Lang.GetText("BetterFollow-ForcedFollowConfig", CommandStr), ref ForcedFollow))
        {
            UpdateConfig("ForcedFollow", ForcedFollow);
            if (ForcedFollow)
                CommandManager.AddCommand(CommandStr,
                                          new CommandInfo(OnCommand)
                                          {
                                              HelpMessage = Service.Lang.GetText("BetterFollow-CommandDesc", CommandStr)
                                          });
            else
                CommandManager.RemoveCommand(CommandStr);
        }

        if (ForcedFollow)
        {
            ImGui.Indent();
            ImGui.Text(Service.Lang.GetText("BetterFollow-CommandDesc"));
            ImGui.Unindent();
        }
    }

    private void FollowOnFramework(IFramework _)
    {
        if (!_FollowStatus) return;
        var followObject = Service.ObjectTable.SearchById(_LastFollowObjectId);
        if (followObject == null) return;
        if (Service.Condition[ConditionFlag.InFlight])
        {
            TaskManager.Abort();
        }

        if (followObject.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Player)
        {
            if (MountWhenFollowMount &&
                ((FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)followObject.Address)->IsMounted() &&
                Flags.CanMount)
            {
                StopFollow();
                ActionManager.Instance()->UseAction(ActionType.GeneralAction, 9);
                return;
            }

            if (FlyingWhenFollowFlying && ((CharacterFlying*)followObject.Address)->IsFlying != 0 &&
                !Service.Condition[ConditionFlag.InFlight] && Service.Condition[ConditionFlag.Mounted])
            {
                TaskManager.Enqueue(() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 2));
                TaskManager.DelayNext(50);
                TaskManager.Enqueue(() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 2));
                return;
            }

            if (!((FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)followObject.Address)->IsMounted() &&
                Service.Condition[ConditionFlag.Mounted])
            {
                ((FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara*)followObject.Address)->GetStatusManager->
                    RemoveStatus(10);
                ActionManager.Instance()->UseAction(ActionType.GeneralAction, 9);
                return;
            }
        }

        if (MoveType == MoveTypeList.Nvavmesh && _FollowStatus)
        {
            if ( Service.KeyState[VirtualKey.S] || Service.KeyState[VirtualKey.A] || Service.KeyState[VirtualKey.D])
            {
                _FollowStatus = false;
                Service.Chat.Print($"取消跟随。");
                return;
            }

            if (!EzThrottler.Throttle("BetterFollowMove", (int)Delay * 1000)) return;
            if (Vector3.Distance(Service.ClientState.LocalPlayer.Position, followObject.Position) < 5) return;
            if (Service.ClientState.LocalPlayer.IsCasting) return;
            if (Service.ClientState.LocalPlayer.IsDead) return;
            if (!vnavmesh.NavIsReady()) return;

            vnavmesh.PathfindAndMoveTo(followObject.Position, Service.Condition[ConditionFlag.InFlight]);
        }
    }

    private void ReFollowOnFramework(IFramework _)
    {
        // 打断,要放在上面不然delay拉的太高就打断不了了
        if (_enableReFollow && InterruptByConflictKey())
        {
            _enableReFollow = false;
            return;
        }

        if (MoveType == MoveTypeList.System) _FollowStatus = *(int*)_d1 == 4;
        if (!EzThrottler.Throttle("BetterFollow", (int)Delay * 1000)) return;
        if (!AutoReFollow) return;
        if (_FollowStatus) return;
        // 在过图
        if (Flags.BetweenAreas()) return;
        // 自己无了
        if (Service.ClientState.LocalPlayer == null) return;
        // 按键打断
        if (!_enableReFollow)
        {
            _LastFollowObjectName = "无";
            _LastFollowObjectStatus = false;
            return;
        }

        // 在战斗
        if (OnCombatOver && Service.Condition[ConditionFlag.InCombat]) return;
        // 在副本里
        if (!OnDuty && Flags.BoundByDuty()) return;
        // 在读条
        if (Service.ClientState.LocalPlayer.IsCasting) return;
        // 在看剧情
        if (Service.ClientState.LocalPlayer.OnlineStatus.GameData.RowId == 15) return;
        // 在移动
        if (AgentMap.Instance()->IsPlayerMoving == 1) return;
        // 死了
        if (Service.ClientState.LocalPlayer.IsDead) return;
        // 跟随目标无了
        if (Service.ObjectTable.SearchById(_LastFollowObjectId) == null ||
            !Service.ObjectTable.SearchById(_LastFollowObjectId).IsTargetable)
        {
            _LastFollowObjectStatus = false;
            return;
        }

        // 跟随目标换图了并且换了内存地址
        if (Service.ObjectTable.SearchById(_LastFollowObjectId).Address != _LastFollowObjectAddress)
        {
            //更新目标的新地址
            _LastFollowObjectAddress = Service.ObjectTable.SearchById(_LastFollowObjectId).Address;
            NewFollow(_LastFollowObjectAddress);
        }
        else
            ReFollow();
    }

    private void OnCommand(string command, string args)
    {
        var localPlayer = Service.ClientState.LocalPlayer;
        if (localPlayer == null || localPlayer.TargetObject == null) return;
        NewFollow(localPlayer.TargetObject.Address);
    }

    private void ReFollow()
    {
        switch (MoveType)
        {
            case MoveTypeList.System:
                if (_FollowStatus) return;
                if (_LastFollowObjectAddress == 0) return;
                //为了避免换图可能跟随目标上一个图的地点,需要重新把obj地址推进去
                FollowDataPush(_a1_data, _LastFollowObjectAddress);
                SafeMemory.Write(_d1, 4);
                _FollowStatus = true;
                break;
            case MoveTypeList.Nvavmesh:
                if (_FollowStatus) return;
                if (_LastFollowObjectAddress == 0) return;
                _FollowStatus = true;
                break;
        }
    }

    private static void StopFollow()
    {
        switch (MoveType)
        {
            case MoveTypeList.System:
                if (!_FollowStatus) return;
                if (*(int*)_d1 != 4) return;
                SafeMemory.Write(_d1, 1);
                _FollowStatus = false;
                break;
            case MoveTypeList.Nvavmesh:
                if (!vnavmesh.PathIsRunning()) return;
                vnavmesh.PathStop();
                _FollowStatus = false;
                break;
        }
    }

    private void NewFollow(nint objectAddress)
    {
        switch (MoveType)
        {
            case MoveTypeList.System:
                if (_FollowStatus) return;
                if (objectAddress == 0) return;
                FollowStart(_v8, 52, _v5, objectAddress);
                FollowDataPush(_a1_data, objectAddress);
                SafeMemory.Write(_d1, 4);
                _enableReFollow = true;
                _FollowStatus = true;
                break;
            case MoveTypeList.Nvavmesh:
                _LastFollowObjectAddress = objectAddress;
                _LastFollowObjectId = (*(GameObject*)objectAddress).ObjectID;
                _LastFollowObjectName = Service.ObjectTable.SearchById((*(GameObject*)objectAddress).ObjectID).Name
                                               .ToString();
                _LastFollowObjectStatus = true;
                _enableReFollow = true;
                _FollowStatus = true;
                break;
        }
    }

    private void FollowData(ulong a1, nint a2)
    {
        FollowDataHook.Original(a1, a2);
        _LastFollowObjectAddress = a2;
        _LastFollowObjectId = (*(GameObject*)a2).ObjectID;
        _LastFollowObjectName = Service.ObjectTable.SearchById((*(GameObject*)a2).ObjectID).Name.ToString();
        if (MoveType == MoveTypeList.Nvavmesh)
        {
            WindowsKeypress.SendKeypress(Keys.W);
            Service.Chat.Print("开始使用Vnavmesh跟随");
        }
        _LastFollowObjectStatus = true;
        _enableReFollow = true;
        _FollowStatus = true;
    }

    private void OnChatMessage(
        XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if (type!=XivChatType.SystemMessage) return;
        var msg = message.TextValue;
        if (Service.PayloadText.EndFollow.All(x => msg.Contains(x)))
        {
            isHandled = true;
        }
    }


    public override void Uninit()
    {
        CommandManager.RemoveCommand(CommandStr);
        Service.Framework.Update -= FollowOnFramework;
        Service.Framework.Update -= ReFollowOnFramework;
        Service.Chat.ChatMessage -= OnChatMessage;
        _enableReFollow = false;
        _FollowStatus = false;

        base.Uninit();
    }
}
