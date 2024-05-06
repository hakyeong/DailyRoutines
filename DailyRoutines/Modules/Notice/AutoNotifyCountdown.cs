using System.Collections.Generic;
using System.Linq;
using DailyRoutines.Helpers;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using ImGuiNET;
using DailyRoutines.Notifications;
using Lumina.Excel.GeneratedSheets;
using PayloadType = Lumina.Text.Payloads.PayloadType;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoNotifyCountdownTitle", "AutoNotifyCountdownDescription", ModuleCategories.通知)]
public class AutoNotifyCountdown : DailyModuleBase
{
    public override string? Author { get; set; } = "HSS";
    private static bool ConfigOnlyNotifyWhenBackground;
    private static List<string>? Countdown;

    public override void Init()
    {
        AddConfig("OnlyNotifyWhenBackground", true);
        ConfigOnlyNotifyWhenBackground = GetConfig<bool>("OnlyNotifyWhenBackground");

        Countdown ??= LuminaCache.GetRow<LogMessage>(5255).Text.Payloads
                                 .Where(x => x.PayloadType == PayloadType.Text)
                                 .Select(text => text.RawString).ToList();

        Service.Chat.ChatMessage += OnChatMessage;
    }

    public override void ConfigUI()
    {
        PreviewImageWithHelpText(Service.Lang.GetText("AutoNotifyCountdown-NotificationMessageHelp"),
                                 "https://gh.atmoomen.top/DailyRoutines/main/Assets/Images/AutoNotifyCountdown-1.png");

        if (ImGui.Checkbox(Service.Lang.GetText("OnlyNotifyWhenBackground"),
                           ref ConfigOnlyNotifyWhenBackground))
            UpdateConfig("OnlyNotifyWhenBackground", ConfigOnlyNotifyWhenBackground);
    }

    private static void OnChatMessage(
        XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if (ConfigOnlyNotifyWhenBackground && IsGameForeground()) return;
        var uintType = (uint)type;
        if (uintType != 185) return;

        var msg = message.TextValue;
        if (Countdown.All(s => msg.Contains(s)))
            WinToast.Notify(Service.Lang.GetText("AutoNotifyCountdown-NotificationTitle"), message.ExtractText());
    }

    public override void Uninit()
    {
        Service.Chat.ChatMessage -= OnChatMessage;

        base.Uninit();
    }
}
