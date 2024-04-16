using System.Diagnostics;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;

namespace DailyRoutines.Modules;

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
    private static ulong _FollowStartA1;
    private static nint _LastFollowObjectAddress;
    private static uint _LastFollowObjectId;
    private static nint _BassAddress;
    private static nint _a1;
    private static nint _a1_data;
    private static nint _v5;
    private static nint _d1;
    private static nint _a2;

    // Hook
    private delegate void FollowA1Delegate(ulong a1, ulong a2);

    [Signature("E8 ?? ?? ?? ?? C6 03 ?? 48 81 C6", DetourName = nameof(UpdateFollowA1))]
    private readonly Hook<FollowA1Delegate>? FollowA1Hook;

    private delegate void FollowDataDelegate(ulong a1, nint a2);

    [Signature("E8 ?? ?? ?? ?? EB ?? 48 81 C1 ?? ?? ?? ?? E8 ?? ?? ?? ?? EB", DetourName = nameof(FollowData))]
    private readonly Hook<FollowDataDelegate>? FollowDataHook;

    [Signature(
        "40 53 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 48 8B D9 48 8D 4C 24 ?? E8 ?? ?? ?? ?? 48 8B D0 48 8D 8B ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8D 4C 24 ?? E8 ?? ?? ?? ?? 48 8B 4C 24 ?? BA ?? ?? ?? ?? 41 B8 ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B 8C 24 ?? ?? ?? ?? 48 33 CC E8 ?? ?? ?? ?? 48 81 C4 ?? ?? ?? ?? 5B C3 CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC CC 48 89 5C 24")]
    private readonly delegate* unmanaged<ulong, uint, ulong, nint, ulong> FollowStart;

    [Signature("48 89 5C 24 ?? 57 48 83 EC ?? 48 8B F9 48 8B DA 48 8B CA E8 ?? ?? ?? ?? 44 8B 00")]
    private readonly delegate* unmanaged<nint, nint, ulong> FollowDataPush;

    // 配置
    private static bool AutoReFollow = true;
    private static bool OnCombatOver;
    private static bool OnDuty;
    private static bool ForcedFollow;
    private static float Delay = 0.5f;

    private const string CommandStr = "/pdrfollow";

    public override void Init()
    {
        #region Config
        AddConfig(this, "AutoReFollow", AutoReFollow);
        AutoReFollow = GetConfig<bool>(this, "AutoReFollow");

        AddConfig(this, "OnCombatOver", OnCombatOver);
        OnCombatOver = GetConfig<bool>(this, "OnCombatOver");

        AddConfig(this, "Delay", Delay);
        Delay = GetConfig<float>(this, "Delay");

        AddConfig(this, "OnDuty", OnDuty);
        OnDuty = GetConfig<bool>(this, "OnDuty");

        AddConfig(this, "ForcedFollow", ForcedFollow);
        ForcedFollow = GetConfig<bool>(this, "ForcedFollow");
        #endregion
        
        _a1_data = 0;
        _v5 = 0;
        _d1 = 0;

        _BassAddress = Process.GetCurrentProcess().MainModule.BaseAddress;
        _a1 = _BassAddress + 0x21CF590;
        _a2 = _BassAddress + 0x21CF9C0;
        _v5 = _BassAddress + 0x21CFB60;
        _a1_data = _a1 + 0x450;
        _d1 = _a1 + 1369;
        _FollowStatus = *(int*)_d1 == 4;

        Service.Hook.InitializeFromAttributes(this);
        FollowA1Hook?.Enable();
        FollowDataHook?.Enable();

        Service.Framework.Update += OnFramework;

        if (ForcedFollow)
            CommandManager.AddCommand(CommandStr, 
                                      new CommandInfo(OnCommand) { HelpMessage = Service.Lang.GetText("BetterFollow-CommandDesc", CommandStr) });
    }

    public override void ConfigUI()
    {
        ConflictKeyText();

        ImGui.Spacing();

        if (ImGui.Checkbox(Service.Lang.GetText("BetterFollow-AutoReFollowConfig"), ref AutoReFollow))
            UpdateConfig(this, "AutoReFollow", AutoReFollow);
        if (AutoReFollow)
        {
            ImGui.Indent();
            ImGui.Text(Service.Lang.GetText("BetterFollow-Status", _enableReFollow, _LastFollowObjectName, _LastFollowObjectStatus));

            if (ImGui.Checkbox(Service.Lang.GetText("BetterFollow-OnCombatOverConfig"), ref OnCombatOver))
                UpdateConfig(this, "OnCombatOver", OnCombatOver);
            
            if (ImGui.Checkbox(Service.Lang.GetText("BetterFollow-OnDutyConfig"), ref OnDuty))
                UpdateConfig(this, "OnCombatOver", OnDuty);

            ImGui.SetNextItemWidth(300f);
            ImGui.SliderFloat(Service.Lang.GetText("BetterFollow-DelayConfig"), ref Delay, 0.5f, 5f, "%.1f");
            if (ImGui.IsItemDeactivatedAfterEdit())
                UpdateConfig(this, "Delay", Delay);

            ImGui.Unindent();
        }

        if (ImGui.Checkbox(Service.Lang.GetText("BetterFollow-ForcedFollowConfig", CommandStr), ref ForcedFollow))
        {
            UpdateConfig(this, "ForcedFollow", ForcedFollow);
            if (ForcedFollow)
                CommandManager.AddCommand(CommandStr, 
                                          new CommandInfo(OnCommand) 
                                              { HelpMessage = Service.Lang.GetText("BetterFollow-CommandDesc", CommandStr) });
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


    private void OnFramework(IFramework _)
    {
        if (!EzThrottler.Throttle("BetterFollow", (int)Delay * 1000)) return;

        // 打断
        if (_enableReFollow && InterruptByConflictKey())
        {
            _enableReFollow = false;
            return;
        }

        _FollowStatus = *(int*)_d1 == 4;
        if (!AutoReFollow) return;
        if (_FollowStatus) return;
        // 在过图
        if (Flags.BetweenAreas()) return;

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

        // 跟随目标换图了
        if (Service.ObjectTable.SearchById(_LastFollowObjectId).Address != _LastFollowObjectAddress)
        {
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

        NewFollow(Service.ClientState.LocalPlayer.TargetObject.Address);
    }

    private void ReFollow()
    {
        if (_FollowStatus) return;
        if (_LastFollowObjectAddress == 0 && _FollowStartA1 == 0) return;

        FollowDataPush(_a1_data, _LastFollowObjectAddress);
        SafeMemory.Write(_d1, 4);
        _FollowStatus = true;
    }

    private static void StopFollow()
    {
        if (!_FollowStatus) return;

        SafeMemory.Write(_d1, 1);
        _FollowStatus = false;
    }

    private void NewFollow(nint objectAddress)
    {
        if (_FollowStatus) return;
        if (objectAddress == 0 && _FollowStartA1 == 0) return;

        FollowStart(_FollowStartA1, 52, *(ulong*)_v5, objectAddress);
        FollowDataPush(_a1_data, objectAddress);
        SafeMemory.Write(_d1, 4);
        _enableReFollow = true;
        _FollowStatus = true;
    }


    private void UpdateFollowA1(ulong a1, ulong a2)
    {
        if (_FollowStartA1 < a1) _FollowStartA1 = a1;
        FollowA1Hook.Original(a1, a2);
    }

    private void FollowData(ulong a1, nint a2)
    {
        _LastFollowObjectAddress = a2;
        _LastFollowObjectId = (*(GameObject*)a2).ObjectID;
        _LastFollowObjectName = Service.ObjectTable.SearchById((*(GameObject*)a2).ObjectID).Name.ToString();
        _LastFollowObjectStatus = true;
        _enableReFollow = true;
        FollowDataHook.Original(a1, a2);
    }

    public override void Uninit()
    {
        CommandManager.RemoveCommand(CommandStr);
        Service.Framework.Update -= OnFramework;
        _enableReFollow = false;
        _FollowStatus = false;

        base.Uninit();
    }
}
