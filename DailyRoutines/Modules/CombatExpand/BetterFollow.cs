using System.Collections.Generic;
using DailyRoutines.Infos;
using DailyRoutines.IPC;
using DailyRoutines.Managers;
using Dalamud;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using ECommons.Automation;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;
using System.Numerics;
using Dalamud.Game.ClientState.Keys;
using Newtonsoft.Json;
using GameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace DailyRoutines.Modules;

[ModuleDescription("BetterFollowTitle", "BetterFollowDescription", ModuleCategories.CombatExpand)]
public unsafe class BetterFollow : DailyModuleBase
{
    public override string? Author { get; set; } = "HSS";

    // 配置
    private class Config : ModuleConfiguration
    {
        public bool AutoReFollow = true;
        public bool OnCombatOver = true;
        public bool OnDuty = false;
        public bool ForcedFollow = false;
        public float Delay = 0.5f;
        public bool MountWhenFollowMount = true;
        public bool UnMountWhenFollowUnMount = false;
        public bool FlyingWhenFollowFlying = true;
        public int FollowDistance = 5;
        public int ReFollowDistance = 3;
        public MoveTypeList MoveType = MoveTypeList.System;
    }

    private Config ModuleConfig = null!;

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


    private static vnavmeshIPC? vnavmesh;
    public const string CommandStr = "/pdrfollow";

    public override void Init()
    {
        ModuleConfig = LoadConfig<Config>() ?? new();

        #region Data

        SafeMemory.Write(_a1 + 1189, 0);
        SafeMemory.Write(_a1 + 1369, 0);
        _FollowStatus = false;
        _enableReFollow = false;
        _LastFollowObjectAddress = 0;
        _LastFollowObjectId = 0;
        _a1 = Service.SigScanner.GetStaticAddressFromSig(
            "48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? EB ?? 48 8D 15 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 84 DB");
        _v5 = *(nint*)Service.SigScanner.GetStaticAddressFromSig("4C 8B 35 ?? ?? ?? ?? 48 3B D0");
        _a1_data = _a1 + 0x450;
        _d1 = _a1 + 1369;
        _v8 = *(nint*)((*(ulong*)Service.SigScanner.GetStaticAddressFromSig(
                            "48 8B 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B C8 48 8B 10 FF 52 ?? 4C 8B CE")) + 0x2B60) +
              0x19c8;

        #endregion

        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = false };
        Service.Hook.InitializeFromAttributes(this);
        FollowDataHook?.Enable();
        vnavmesh ??= Service.IPCManager.Load<vnavmeshIPC>(this);
        if (vnavmesh == null) ModuleConfig.MoveType = MoveTypeList.System;
        Service.FrameworkManager.Register(OnFramework);
        if (ModuleConfig.ForcedFollow)
            Service.CommandManager.AddCommand(CommandStr,
                                      new CommandInfo(OnCommand)
                                      {
                                          HelpMessage =
                                              Service.Lang.GetText("BetterFollow-CommandDesc", CommandStr)
                                      });
    }

    public override void ConfigUI()
    {
        var disabledFlag = (_FollowStatus || vnavmesh == null);
        if (disabledFlag)
        {
            ImGui.BeginDisabled();
        }

        ImGui.Text(Service.Lang.GetText("BetterFollow-MoveType"));
        ImGui.SameLine();
        ImGui.SetNextItemWidth(200f);
        if (ImGui.BeginCombo("###MoveTypoCombo", MoveTypeLoc[ModuleConfig.MoveType]))
        {
            foreach (var mode in MoveTypeLoc)
            {
                if (ImGui.Selectable(mode.Value, mode.Key == ModuleConfig.MoveType))
                {
                    ModuleConfig.MoveType = mode.Key;
                    SaveConfig(ModuleConfig);
                }
            }

            ImGui.EndCombo();
        }

        if (disabledFlag)
        {
            ImGui.EndDisabled();
        }

        if (ImGui.Button(Service.Lang.GetText("BetterFollow-ReloadIPC")))
        {
            vnavmesh = Service.IPCManager.Load<vnavmeshIPC>(this);
        }

        if (ModuleConfig.MoveType == MoveTypeList.Nvavmesh)
        {
            ImGui.SameLine();
            ImGui.Text(Service.Lang.GetText("BetterFollow-NvavmeshFollowDesc"));
            ImGui.Text(Service.Lang.GetText("BetterFollow-FollowDistance"));
            ImGui.SameLine();
            ImGui.SetNextItemWidth(300f);
            ImGui.SliderInt("###BetterFollow-FollowDistance", ref ModuleConfig.FollowDistance, 1, 15);
            if (ImGui.IsItemDeactivatedAfterEdit())
                SaveConfig(ModuleConfig);
        }

        ImGui.Spacing();

        ImGui.Text(Service.Lang.GetText("BetterFollow-DelayConfig"));
        ImGui.SameLine();
        ImGui.SetNextItemWidth(300f);
        ImGui.SliderFloat("###BetterFollow-DelayConfig", ref ModuleConfig.Delay, 0.5f, 5f, "%.1f");
        if (ImGui.IsItemDeactivatedAfterEdit())
            SaveConfig(ModuleConfig);

        if (ImGui.Checkbox(Service.Lang.GetText("AutoMount-MountWhenFollowMount"),
                           ref ModuleConfig.MountWhenFollowMount))
            SaveConfig(ModuleConfig);

        if (ModuleConfig.MountWhenFollowMount)
        {
            ImGui.Indent();
            if (ImGui.Checkbox(Service.Lang.GetText("AutoMount-FlyingWhenFollowFlying"),
                               ref ModuleConfig.FlyingWhenFollowFlying))
                SaveConfig(ModuleConfig);
            ImGui.Unindent();
        }

        if (ImGui.Checkbox(Service.Lang.GetText("AutoMount-UnMountWhenFollowUnMount"),
                           ref ModuleConfig.UnMountWhenFollowUnMount))
            SaveConfig(ModuleConfig);

        if (ImGui.Checkbox(Service.Lang.GetText("BetterFollow-AutoReFollowConfig"), ref ModuleConfig.AutoReFollow))
            SaveConfig(ModuleConfig);
        if (ModuleConfig.AutoReFollow)
        {
            ImGui.Indent();
            ConflictKeyText();
            ImGui.Text(Service.Lang.GetText("BetterFollow-Status", _enableReFollow, _LastFollowObjectName,
                                            _LastFollowObjectStatus));

            if (ImGui.Checkbox(Service.Lang.GetText("BetterFollow-OnCombatOverConfig"), ref ModuleConfig.OnCombatOver))
                SaveConfig(ModuleConfig);

            if (ImGui.Checkbox(Service.Lang.GetText("BetterFollow-OnDutyConfig"), ref ModuleConfig.OnDuty))
                SaveConfig(ModuleConfig);
            ImGui.Text(Service.Lang.GetText("BetterFollow-ReFollowDistance"));
            ImGui.SameLine();
            ImGui.SetNextItemWidth(300f);
            ImGui.SliderInt("###BetterFollow-ReFollowDistance", ref ModuleConfig.ReFollowDistance, 1, 15);
            if (ImGui.IsItemDeactivatedAfterEdit())
                SaveConfig(ModuleConfig);
            ImGui.Unindent();
        }

        if (ImGui.Checkbox(Service.Lang.GetText("BetterFollow-ForcedFollowConfig", CommandStr),
                           ref ModuleConfig.ForcedFollow))
        {
            SaveConfig(ModuleConfig);
            if (ModuleConfig.ForcedFollow)
                Service.CommandManager.AddCommand(CommandStr,
                                                  new CommandInfo(OnCommand)
                                                  {
                                                      HelpMessage =
                                                          Service.Lang.GetText("BetterFollow-CommandDesc",
                                                                               CommandStr)
                                                  });
            else
                Service.CommandManager.RemoveCommand(CommandStr);
        }

        if (ModuleConfig.ForcedFollow)
        {
            ImGui.Indent();
            ImGui.Text(Service.Lang.GetText("BetterFollow-CommandDesc"));
            ImGui.Unindent();
        }
    }

    private void OnFramework(IFramework _)
    {
        /*-------------------------------需要实时处理的模块-------------------------------*/
        //处理打断重新跟随逻辑
        if (_enableReFollow && InterruptByConflictKey())
        {
            _enableReFollow = false;
            _LastFollowObjectName = "无";
            _LastFollowObjectStatus = false;
            return;
        }

        var followObject = Service.ObjectTable.SearchById(_LastFollowObjectId);
        //处理打断Vnavmesh跟随逻辑
        if (ModuleConfig.MoveType == MoveTypeList.Nvavmesh && _FollowStatus)
        {
            //目标无了
            if (followObject == null) StopFollow(true);
            //自行移动了
            if (Service.KeyState[VirtualKey.W] || Service.KeyState[VirtualKey.S] || Service.KeyState[VirtualKey.A] ||
                Service.KeyState[VirtualKey.D]) StopFollow(true);
            //过图了或者死了
            if (Service.ClientState.LocalPlayer.IsDead || Flags.BetweenAreas()) StopFollow(true);
            //进剧情了
            if (Service.ClientState.LocalPlayer.OnlineStatus.GameData.RowId == 15) StopFollow(true);
        }

        //更新系统跟随状态
        if (ModuleConfig.MoveType == MoveTypeList.System) _FollowStatus = *(int*)_d1 == 4;

        //如果已经进入飞行状态就别跳了
        if (Service.Condition[ConditionFlag.InFlight]) TaskManager.Abort();

        //处理上坐骑和起飞逻辑
        if (_FollowStatus && followObject != null &&
            followObject.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Player)
        {
            //跟随目标上坐骑
            if (ModuleConfig.MountWhenFollowMount &&
                ((FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)followObject.Address)->IsMounted() &&
                Flags.CanMount)
            {
                StopFollow(false);
                ActionManager.Instance()->UseAction(ActionType.GeneralAction, 9);
                return;
            }

            //跟随目标起飞
            if (ModuleConfig.FlyingWhenFollowFlying && ((CharacterFlying*)followObject.Address)->IsFlying != 0 &&
                !Service.Condition[ConditionFlag.InFlight] && Service.Condition[ConditionFlag.Mounted])
            {
                TaskManager.Enqueue(() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 2));
                TaskManager.DelayNext(50);
                TaskManager.Enqueue(() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 2));
                return;
            }

            //跟随目标下坐骑
            if (ModuleConfig.UnMountWhenFollowUnMount &&
                !((FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)followObject.Address)->IsMounted() &&
                Service.Condition[ConditionFlag.Mounted])
            {
                ((FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara*)followObject.Address)->GetStatusManager->
                    RemoveStatus(10);
                ActionManager.Instance()->UseAction(ActionType.GeneralAction, 9);
                return;
            }
        }

        /*-------------------------------不需要实时处理的模块-------------------------------*/
        if (!EzThrottler.Throttle("BetterFollow", (int)ModuleConfig.Delay * 1000)) return;

        //处理移动逻辑
        if (ModuleConfig.MoveType == MoveTypeList.Nvavmesh && _FollowStatus && followObject != null)
        {
            if (Vector3.Distance(Service.ClientState.LocalPlayer.Position, followObject.Position) <
                ModuleConfig.FollowDistance) return;
            if (Service.ClientState.LocalPlayer.IsCasting) return;
            if (!vnavmesh.NavIsReady()) return;
            vnavmesh.PathfindAndMoveTo(followObject.Position, Service.Condition[ConditionFlag.InFlight]);
        }

        //处理重新跟随逻辑
        if (!_enableReFollow) return;
        if (!ModuleConfig.AutoReFollow || _FollowStatus) return;
        // 在战斗
        if (ModuleConfig.OnCombatOver && Service.Condition[ConditionFlag.InCombat]) return;
        // 在副本里
        if (!ModuleConfig.OnDuty && Flags.BoundByDuty()) return;
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
        //距离不够
        if (Vector3.Distance(Service.ClientState.LocalPlayer.Position, Service.ObjectTable.SearchById(_LastFollowObjectId).Position) <
            ModuleConfig.ReFollowDistance) return;
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
        switch (ModuleConfig.MoveType)
        {
            case MoveTypeList.System:
                if (_FollowStatus) return;
                if (_LastFollowObjectAddress == 0) return;
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

    private void StopFollow(bool message = false)
    {
        switch (ModuleConfig.MoveType)
        {
            case MoveTypeList.System:
                if (!_FollowStatus) return;
                if (*(int*)_d1 != 4) return;
                SafeMemory.Write(_a1 + 1189, 0);
                SafeMemory.Write(_a1 + 1369, 0);
                _FollowStatus = false;
                break;
            case MoveTypeList.Nvavmesh:
                _FollowStatus = false;
                if (!vnavmesh.PathIsRunning()) return;
                vnavmesh.PathStop();
                break;
        }

        if (message) Service.Chat.Print($"取消跟随。");
    }

    private void NewFollow(nint objectAddress)
    {
        if (_FollowStatus) return;
        if (objectAddress == 0) return;
        _LastFollowObjectAddress = objectAddress;
        _LastFollowObjectId = (*(GameObject*)objectAddress).ObjectID;
        _LastFollowObjectName = Service.ObjectTable.SearchById((*(GameObject*)objectAddress).ObjectID).Name
                                       .ToString();
        switch (ModuleConfig.MoveType)
        {
            case MoveTypeList.System:
                FollowStart(_v8, 52, _v5, objectAddress);
                FollowDataPush(_a1_data, objectAddress);
                SafeMemory.Write(_d1, 4);
                break;
            case MoveTypeList.Nvavmesh:
                break;
        }

        _LastFollowObjectStatus = true;
        _enableReFollow = true;
        _FollowStatus = true;
    }

    private void FollowData(ulong a1, nint a2)
    {
        FollowDataHook.Original(a1, a2);

        //获取跟随对象
        _LastFollowObjectAddress = a2;
        _LastFollowObjectId = (*(GameObject*)a2).ObjectID;
        _LastFollowObjectName = Service.ObjectTable.SearchById((*(GameObject*)a2).ObjectID).Name.ToString();
        if (ModuleConfig.MoveType == MoveTypeList.Nvavmesh)
        {
            //停止系统跟随
            TaskManager.DelayNext(100);
            TaskManager.Enqueue(() =>
            {
                SafeMemory.Write(_a1 + 1189, 0);
                SafeMemory.Write(_a1 + 1369, 0);
            });
            Service.Chat.Print("开始使用Vnavmesh跟随");
        }

        _LastFollowObjectStatus = true;
        _enableReFollow = true;
        _FollowStatus = true;
    }

    private enum MoveTypeList
    {
        System,
        Nvavmesh
    }

    private static readonly Dictionary<MoveTypeList, string> MoveTypeLoc = new Dictionary<MoveTypeList, string>
    {
        { MoveTypeList.System, Service.Lang.GetText("BetterFollow-SystemFollow") },
        { MoveTypeList.Nvavmesh, Service.Lang.GetText("BetterFollow-NvavmeshFollow") },
    };


    public override void Uninit()
    {
        Service.CommandManager.RemoveCommand(CommandStr);
        _enableReFollow = false;
        _FollowStatus = false;

        base.Uninit();
    }
}
