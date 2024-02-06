using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Colors;
using Dalamud.Interface;
using ECommons.ImGuiMethods;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoNotifyMessagesTitle", "AutoNotifyMessagesDescription", ModuleCategories.Notice)]
public class AutoNotifyMessages : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithConfigUI => true;

    private static bool ConfigOnlyNotifyWhenBackground;
    private static HashSet<XivChatType> ConfigValidChatTypes = [];

    private static string SearchChatTypesContent = string.Empty;

    private static readonly Dictionary<XivChatType, string> ChatTypesLoc = new()
    {
        { XivChatType.Notice, "系统通知" },
        { XivChatType.SystemError, "系统错误" },
        { XivChatType.SystemMessage, "系统消息" },
        { XivChatType.ErrorMessage, "错误消息" },
        { XivChatType.RetainerSale, "雇员出售" },
        { XivChatType.Say, "说话" },
        { XivChatType.Yell, "呼喊" },
        { XivChatType.Shout, "喊话" },
        { XivChatType.TellIncoming, "悄悄话" },
        { XivChatType.Party, "小队" },
        { XivChatType.CrossParty, "跨服小队" },
        { XivChatType.Alliance, "团队" },
        { XivChatType.FreeCompany, "部队" },
        { XivChatType.PvPTeam, "战队" },
        { XivChatType.Echo, "默语" },
        { XivChatType.NoviceNetwork, "新人频道" },
        { XivChatType.StandardEmote, "情感动作" },
        { XivChatType.CustomEmote, "自定义情感动作" },
        { XivChatType.Ls1, "通讯贝1" },
        { XivChatType.Ls2, "通讯贝2" },
        { XivChatType.Ls3, "通讯贝3" },
        { XivChatType.Ls4, "通讯贝4" },
        { XivChatType.Ls5, "通讯贝5" },
        { XivChatType.Ls6, "通讯贝6" },
        { XivChatType.Ls7, "通讯贝7" },
        { XivChatType.Ls8, "通讯贝8" },
        { XivChatType.CrossLinkShell1, "跨服贝1" },
        { XivChatType.CrossLinkShell2, "跨服贝2" },
        { XivChatType.CrossLinkShell3, "跨服贝3" },
        { XivChatType.CrossLinkShell4, "跨服贝4" },
        { XivChatType.CrossLinkShell5, "跨服贝5" },
        { XivChatType.CrossLinkShell6, "跨服贝6" },
        { XivChatType.CrossLinkShell7, "跨服贝7" },
        { XivChatType.CrossLinkShell8, "跨服贝8" },
    };

    public void Init()
    {
        Service.Config.AddConfig(this, "OnlyNotifyWhenBackground", true);
        ConfigOnlyNotifyWhenBackground = Service.Config.GetConfig<bool>(this, "OnlyNotifyWhenBackground");

        Service.Config.AddConfig(this, "ValidChatTypes", new HashSet<XivChatType> { XivChatType.TellIncoming });
        ConfigValidChatTypes = Service.Config.GetConfig<HashSet<XivChatType>>(this, "ValidChatTypes");

        Service.Chat.ChatMessage += OnChatMessage;
    }

    public void ConfigUI()
    {
        var infoImageState = ThreadLoadImageHandler.TryGetTextureWrap(
            "https://raw.githubusercontent.com/AtmoOmen/DailyRoutines/main/imgs/AutoNotifyMessages-1.png",
            out var imageHandler);

        ImGui.TextColored(ImGuiColors.DalamudOrange, $"{Service.Lang.GetText("AutoNotifyMessages-NotificationMessageHelp")}:");

        ImGui.SameLine();
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.TextDisabled(FontAwesomeIcon.InfoCircle.ToIconString());
        ImGui.PopFont();

        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            if (infoImageState)
                ImGui.Image(imageHandler.ImGuiHandle, new Vector2(450, 193));
            else
                ImGui.TextDisabled($"{Service.Lang.GetText("ImageLoading")}...");
            ImGui.EndTooltip();
        }

        if (ImGui.Checkbox(Service.Lang.GetText("AutoNotifyMessages-OnlyWhenBackground"), ref ConfigOnlyNotifyWhenBackground))
        {
            Service.Config.UpdateConfig(this, "OnlyNotifyWhenBackground", ConfigOnlyNotifyWhenBackground);
        }

        ImGui.SetNextItemWidth(400f);
        if (ImGui.BeginCombo("###SelectChatTypesCombo", Service.Lang.GetText("AutoNotifyMessages-SelectedTypesAmount", ConfigValidChatTypes.Count), ImGuiComboFlags.HeightLarge))
        {
            ImGui.SetNextItemWidth(-1f);
            ImGui.InputTextWithHint("###ChatTypeSelectInput", $"{Service.Lang.GetText("PleaseSearch")}...", ref SearchChatTypesContent, 50);

            ImGui.Separator();

            foreach (XivChatType chatType in Enum.GetValues(typeof(XivChatType)))
            {
                if (!ChatTypesLoc.ContainsKey(chatType)) continue;
                var loc = ChatTypesLoc[chatType];

                if (!string.IsNullOrEmpty(SearchChatTypesContent) && !loc.Contains(SearchChatTypesContent, StringComparison.OrdinalIgnoreCase)) continue;

                var existed = ConfigValidChatTypes.Contains(chatType);
                if (ImGui.Checkbox(loc, ref existed))
                {
                    if (!ConfigValidChatTypes.Remove(chatType))
                        ConfigValidChatTypes.Add(chatType);

                    Service.Config.UpdateConfig(this, "ValidChatTypes", ConfigValidChatTypes);
                }
            }
            ImGui.EndCombo();
        }
    }

    public void OverlayUI() { }

    private void OnChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if (!ConfigValidChatTypes.Contains(type)) return;

        var locState = ChatTypesLoc.TryGetValue(type, out var prefix);
        if (ConfigOnlyNotifyWhenBackground)
        {
            if (!HelpersOm.IsGameForeground())
                Service.Notification.ShowWindowsToast($"[{(locState ? prefix : type)}]  {sender.ExtractText()}", message.ExtractText());
        }
        else
        {
            Service.Notification.ShowWindowsToast($"[{(locState ? prefix : type)}]  {sender.ExtractText()}", message.ExtractText());
        }
    }

    public void Uninit()
    {
        Service.Chat.ChatMessage -= OnChatMessage;
    }
}

