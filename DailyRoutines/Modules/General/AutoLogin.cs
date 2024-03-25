using System.Runtime.InteropServices;
using ClickLib;
using DailyRoutines.Clicks;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Interface.Utility;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoLoginTitle", "AutoLoginDescription", ModuleCategories.General)]
public class AutoLogin : DailyModuleBase
{
    private static bool HasLoginOnce;

    private static string ConfigSelectedServer = string.Empty;
    private static int ConfigSelectedCharaIndex = -1;

    public override void Init()
    {
        AddConfig(this, "SelectedServer", string.Empty);
        AddConfig(this, "SelectedCharaIndex", 0);

        ConfigSelectedServer = GetConfig<string>(this, "SelectedServer");
        ConfigSelectedCharaIndex = GetConfig<int>(this, "SelectedCharaIndex");

        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 20000, ShowDebug = false };
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "_TitleMenu", OnTitleMenu);
    }

    public override void ConfigUI()
    {
        ConflictKeyText();

        ImGui.AlignTextToFramePadding();
        ImGui.Text($"{Service.Lang.GetText("AutoLogin-ServerName")}:");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(100f * ImGuiHelpers.GlobalScale);
        if (ImGui.InputText("##AutoLogin-EnterServerName", ref ConfigSelectedServer, 16,
                            ImGuiInputTextFlags.EnterReturnsTrue))
        {
            if (TryGetWorldByName(ConfigSelectedServer, out _))
            {
                UpdateConfig(this, "SelectedServer", ConfigSelectedServer);
                HasLoginOnce = false;
            }
            else
            {
                Service.Chat.PrintError(
                    Service.Lang.GetText("AutoLogin-ServerNotFoundErrorMessage", ConfigSelectedServer));
                ConfigSelectedServer = string.Empty;
            }
        }

        ImGui.SameLine();
        ImGui.Spacing();

        ImGui.SameLine();
        ImGui.Text($"{Service.Lang.GetText("AutoLogin-CharacterIndex")}:");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(100f * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("##AutoLogin-EnterCharaIndex", ref ConfigSelectedCharaIndex, 1, 1,
                           ImGuiInputTextFlags.EnterReturnsTrue))
        {
            if (ConfigSelectedCharaIndex is < 0 or > 8) ConfigSelectedCharaIndex = 0;
            else
            {
                UpdateConfig(this, "SelectedCharaIndex", ConfigSelectedCharaIndex);
                HasLoginOnce = false;
            }
        }

        ImGuiOm.TooltipHover(Service.Lang.GetText("AutoLogin-CharaIndexInputTooltip"));
    }

    private void OnTitleMenu(AddonEvent eventType, AddonArgs? addonInfo)
    {
        if (string.IsNullOrEmpty(ConfigSelectedServer) || ConfigSelectedCharaIndex == -1) return;
        if (HasLoginOnce) return;

        if (InterruptByConflictKey()) return;

        TaskManager.Enqueue(SelectStartGame);
    }

    private unsafe bool? SelectStartGame()
    {
        if (InterruptByConflictKey()) return true;

        var agent = AgentModule.Instance()->GetAgentByInternalId(AgentId.Lobby);
        if (agent == null) return false;

        AgentManager.SendEvent(agent, 0, 1);
        TaskManager.Enqueue(WaitAddonCharaSelectListMenu);
        return true;
    }

    private unsafe bool? WaitAddonCharaSelectListMenu()
    {
        if (InterruptByConflictKey()) return true;

        if (Service.Gui.GetAddonByName("_TitleMenu") != nint.Zero) return false;

        if (TryGetAddonByName<AtkUnitBase>("_CharaSelectListMenu", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(addon))
        {
            var currentServer = addon->GetTextNodeById(8)->NodeText.ExtractText();
            if (string.IsNullOrEmpty(currentServer)) return false;

            if (currentServer == ConfigSelectedServer)
            {
                var handler = new ClickCharaSelectListMenuDR();
                handler.SelectChara(ConfigSelectedCharaIndex);

                TaskManager.Enqueue(() => Click.TrySendClick("select_yes"));
            }
            else
                TaskManager.Enqueue(WaitCharaSelectWorldServer);

            return true;
        }

        return false;
    }

    private unsafe bool? WaitCharaSelectWorldServer()
    {
        if (InterruptByConflictKey()) return true;

        if (TryGetAddonByName<AtkUnitBase>("_CharaSelectWorldServer", out var addon))
        {
            var stringArray = AtkStage.GetSingleton()->GetStringArrayData()[1];
            if (stringArray == null) return false;

            for (var i = 0; i < 16; i++)
            {
                var serverNamePtr = stringArray->StringArray[i];
                if (serverNamePtr == null) continue;

                var serverName = Marshal.PtrToStringUTF8(new nint(serverNamePtr));
                if (serverName.Trim().Length == 0) continue;

                if (serverName != ConfigSelectedServer) continue;

                var handler = new ClickCharaSelectWorldServerDR();
                handler.SelectWorld(i);

                TaskManager.DelayNext(200);
                TaskManager.Enqueue(WaitAddonCharaSelectListMenu);

                return true;
            }
        }

        return false;
    }

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnTitleMenu);
        HasLoginOnce = false;

        base.Uninit();
    }
}
