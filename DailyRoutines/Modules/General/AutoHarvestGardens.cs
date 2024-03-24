using System.Collections.Generic;
using System.Linq;
using ClickLib;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Memory;
using Dalamud.Utility.Signatures;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoHarvestGardensTitle", "AutoHarvestGardensDescription", ModuleCategories.General)]
public unsafe class AutoHarvestGardens : DailyModuleBase
{
    [Signature("E8 ?? ?? ?? ?? 44 0F B6 C3 48 8B D0")]
    private readonly delegate* unmanaged<ulong, GameObject*> GetGameObjectFromObjectID;

    private static uint[] Gardens = [];

    public override void Init()
    {
        Service.Hook.InitializeFromAttributes(this);
        
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 10000, ShowDebug = true };
    }

    public override void ConfigUI()
    {
        ImGui.BeginDisabled(TaskManager.IsBusy);
        if (ImGui.Button(Service.Lang.GetText("Start")))
        {
            Start();
        }
        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("Stop")))
        {
            TaskManager.Abort();
        }
    }

    private void Start()
    {
        var tempSet = new HashSet<uint>();
        foreach (var obj in Service.ObjectTable.Where(x => x.DataId == 2003757))
        {
            tempSet.Add(obj.ObjectId);
            Service.Log.Debug($"ObjID: {obj.ObjectId} DataID: {obj.DataId}");
        }

        Gardens = [.. tempSet];

        var localPlayer = (GameObject*)Service.ClientState.LocalPlayer.Address;
        foreach (var objID in Gardens)
        {
            var gameObj = GetGameObjectFromObjectID(objID);
            var objDistance = HelpersOm.GetGameDistanceFromObject(localPlayer, gameObj);
            if (objDistance > 4) continue;

            TaskManager.Enqueue(() => InteractWithGarden(gameObj));
            TaskManager.Enqueue(ClickPlant);
        }
    }

    private static bool? InteractWithGarden(GameObject* gameObj)
    {
        var targetSystem = TargetSystem.Instance();
        targetSystem->Target = gameObj;
        targetSystem->InteractWithObject(gameObj);

        return TryGetAddonByName<AtkUnitBase>("SelectString", out var addon) && HelpersOm.IsAddonAndNodesReady(addon);
    }

    private static bool? ClickPlant()
    {
        if (TryGetAddonByName<AtkUnitBase>("SelectString", out var addon) && HelpersOm.IsAddonAndNodesReady(addon))
        {
            var content = MemoryHelper.ReadStringNullTerminated((nint)addon->AtkValues[2].String);
            if (!HelpersOm.TryScanSelectStringText(addon, "收获", out var index))
            {
                HelpersOm.TryScanSelectStringText(addon, "取消", out index);
                return Click.TrySendClick($"select_string{index + 1}");
            }

            if (Click.TrySendClick($"select_string{index + 1}")) return true;
        }

        return false;
    }
}
