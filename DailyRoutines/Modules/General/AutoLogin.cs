using System.Runtime.InteropServices;
using ClickLib;
using DailyRoutines.Clicks;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game;
using Dalamud.Game.AddonLifecycle;
using Dalamud.Interface.Internal.Notifications;
using ECommons.Automation;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoLoginTitle", "AutoLoginDescription", ModuleCategories.General)]
public class AutoLogin : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithConfigUI => true;

    private static TaskManager? TaskManager;

    private static bool HasLoginOnce;

    private static string ConfigSelectedServer = string.Empty;
    private static int ConfigSelectedCharaIndex = -1;

    public void Init()
    {
        Service.Config.AddConfig(this, "SelectedServer", string.Empty);
        Service.Config.AddConfig(this, "SelectedCharaIndex", 0);

        ConfigSelectedServer = Service.Config.GetConfig<string>(this, "SelectedServer");
        ConfigSelectedCharaIndex = Service.Config.GetConfig<int>(this, "SelectedCharaIndex");

        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 20000, ShowDebug = true };
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, "_TitleMenu", OnTitleMenu);

        Service.Framework.Update += OnUpdate;
    }

    public void ConfigUI()
    {
        ImGui.Text($"{Service.Lang.GetText("ConflictKey")}: {Service.Config.ConflictKey}");

        ImGui.AlignTextToFramePadding();
        ImGui.Text($"{Service.Lang.GetText("AutoLogin-ServerName")}:");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(150f);
        if (ImGui.InputText("##AutoLogin-EnterServerName", ref ConfigSelectedServer, 16, ImGuiInputTextFlags.EnterReturnsTrue))
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
        ImGui.SetNextItemWidth(150f);
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

    public void OverlayUI() { }

    private static void OnUpdate(Framework framework)
    {
        if (!TaskManager.IsBusy) return;

        if (Service.KeyState[Service.Config.ConflictKey])
        {
            TaskManager.Abort();
            P.PluginInterface.UiBuilder.AddNotification(Service.Lang.GetText("ConflictKey-InterruptMessage"), "Daily Routines", NotificationType.Success);
        }
    }

    private static unsafe void OnTitleMenu(AddonEvent eventType, AddonArgs? addonInfo)
    {
        if (string.IsNullOrEmpty(ConfigSelectedServer) || ConfigSelectedCharaIndex == -1) return;
        if (HasLoginOnce) return;
        if (TryGetAddonByName<AtkUnitBase>("_TitleMenu", out var addon) && IsAddonReady(addon))
        {
            HasLoginOnce = true;
            var handler = new ClickTitleMenuDR();
            handler.Start();

            TaskManager.Enqueue(WaitAddonCharaSelectListMenu);
        }
    }

    private static unsafe bool? WaitAddonCharaSelectListMenu()
    {
        if (TryGetAddonByName<AtkUnitBase>("_CharaSelectListMenu", out var addon) && IsAddonReady(addon))
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

    private static unsafe bool? WaitCharaSelectWorldServer()
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

    public void Uninit()
    {
        Service.Config.Save();
        Service.AddonLifecycle.UnregisterListener(OnTitleMenu);
        TaskManager?.Abort();
        HasLoginOnce = false;
        Service.Framework.Update -= OnUpdate;
    }
}
