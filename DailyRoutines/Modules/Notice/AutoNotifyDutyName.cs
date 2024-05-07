using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using DailyRoutines.Notifications;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Utility;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoNotifyDutyNameTitle", "AutoNotifyDutyNameDescription", ModuleCategories.通知)]
public class AutoNotifyDutyName : DailyModuleBase
{
    private static readonly HttpClient client = new();
    private const string FF14OrgLinkBase = "https://gh.atmoomen.top/novice-network/master/docs/duty/{0}.md";

    private static bool ConfigSendWindowsToast = true;
    private static string _FF14OrgLink = string.Empty;

    public override void Init()
    {
        AddConfig("SendWindowsToast", true);
        ConfigSendWindowsToast = GetConfig<bool>("SendWindowsToast");

        Service.ClientState.TerritoryChanged += OnZoneChange;
    }

    public override void ConfigUI()
    {
        if (ImGui.Checkbox(Service.Lang.GetText("AutoNotifyDutyName-SendWindowsToast"), ref ConfigSendWindowsToast))
            UpdateConfig("SendWindowsToast", ConfigSendWindowsToast);
    }

    private static void OnZoneChange(ushort territory)
    {
        if (!PresetData.Contents.TryGetValue(territory, out var content)) return;
        var contentName = content.Name.RawString;
        _FF14OrgLink = $"https://ff14.org/duty/{content.Content}.htm";

        // Service.PluginInterface.RemoveChatLinkHandler(501);
        // var linkPayload = Service.PluginInterface.AddChatLinkHandler(501, OnFF14OrgLink);
        var message = new SeStringBuilder().Append(DRPrefix()).Append(" ")
                                           .Append(Service.Lang.GetSeString(
                                                       "AutoNotifyDutyName-NoticeMessage", contentName));
        // if (content.Content != 0)
        // {
        //     message.Add(new NewLinePayload())
        //            .Append("            ")
        //            .Add(linkPayload)
        //            .Append("(")
        //            .AddIcon(BitmapFontIcon.NewAdventurer)
        //            .Append("新大陆见闻录)")
        //            .Add(RawPayload.LinkTerminator);
        // }
        Service.Chat.Print(message.Build());
        if (ConfigSendWindowsToast) WinToast.Notify(contentName, contentName);

        // Task.Run(async () => await GetDutyGuide(content.Content));
    }

    private static void OnFF14OrgLink(uint commandID, SeString message)
    {
        if (!string.IsNullOrWhiteSpace(_FF14OrgLink))
            Util.OpenLink(_FF14OrgLink);
    }

    private static async Task GetDutyGuide(ushort dutyID)
    {
        var originalText = await client.GetStringAsync(string.Format(FF14OrgLinkBase, dutyID));
        var plainText = MarkdownToPlainText(originalText);
        if (!string.IsNullOrWhiteSpace(plainText))
            Service.Chat.Print(plainText);
    }

    public override void Uninit()
    {
        Service.ClientState.TerritoryChanged -= OnZoneChange;
        // Service.PluginInterface.RemoveChatLinkHandler(501);

        base.Uninit();
    }
}
