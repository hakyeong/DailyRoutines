using System.Runtime.InteropServices;
using ClickLib;
using DailyRoutines.Clicks;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Interface.Internal.Notifications;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;
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
        Service.Config.AddConfig(this, "SelectedServer", string.Empty);
        Service.Config.AddConfig(this, "SelectedCharaIndex", 0);

        ConfigSelectedServer = Service.Config.GetConfig<string>(this, "SelectedServer");
        ConfigSelectedCharaIndex = Service.Config.GetConfig<int>(this, "SelectedCharaIndex");

        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 20000, ShowDebug = true };
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "_TitleMenu", OnTitleMenu);

        Service.Framework.Update += OnUpdate;
    }

    public override void ConfigUI()
    {
        ImGui.Text($"{Service.Lang.GetText("ConflictKey")}: {Service.Config.ConflictKey}");

        ImGui.AlignTextToFramePadding();
        ImGui.Text($"{Service.Lang.GetText("AutoLogin-ServerName")}:");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(100f * ImGuiHelpers.GlobalScale);
        if (ImGui.InputText("##AutoLogin-EnterServerName", ref ConfigSelectedServer, 16,
                            ImGuiInputTextFlags.EnterReturnsTrue))
        {
            if (TryGetWorldByName(ConfigSelectedServer, out _))
            {
                Service.Config.UpdateConfig(this, "SelectedServer", ConfigSelectedServer);
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
                Service.Config.UpdateConfig(this, "SelectedCharaIndex", ConfigSelectedCharaIndex);
                HasLoginOnce = false;
            }
        }

        ImGuiOm.TooltipHover(Service.Lang.GetText("AutoLogin-CharaIndexInputTooltip"));
    }

    private void OnUpdate(IFramework framework)
    {
        if (!TaskManager.IsBusy) return;

        if (Service.KeyState[Service.Config.ConflictKey])
        {
            TaskManager.Abort();
            P.PluginInterface.UiBuilder.AddNotification(Service.Lang.GetText("ConflictKey-InterruptMessage"),
                                                        "Daily Routines", NotificationType.Success);
        }
    }

    private void OnTitleMenu(AddonEvent eventType, AddonArgs? addonInfo)
    {
        if (string.IsNullOrEmpty(ConfigSelectedServer) || ConfigSelectedCharaIndex == -1) return;
        if (HasLoginOnce) return;
        if (Service.KeyState[Service.Config.ConflictKey]) return;

        TaskManager.Enqueue(SelectStartGame);
    }

    private unsafe bool? SelectStartGame()
    {
        var agent = AgentModule.Instance()->GetAgentByInternalId(AgentId.Lobby);
        if (agent == null) return false;

        AgentManager.SendEvent(agent, 0, 1);
        TaskManager.Enqueue(WaitAddonCharaSelectListMenu);
        return true;
    }

    private unsafe bool? WaitAddonCharaSelectListMenu()
    {
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
        Service.Framework.Update -= OnUpdate;
        Service.AddonLifecycle.UnregisterListener(OnTitleMenu);
        HasLoginOnce = false;

        base.Uninit();
    }
}
