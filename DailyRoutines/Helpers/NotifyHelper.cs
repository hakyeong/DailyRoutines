using System;
using DailyRoutines.Managers;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Internal.Notifications;

namespace DailyRoutines.Helpers;

public static class NotifyHelper
{
    public static void NotificationSuccess(string message) => Service.DalamudNotice.AddNotification(new()
    {
        Title = "Daily Routines",
        Content = message,
        Type = NotificationType.Success,
        InitialDuration = TimeSpan.FromSeconds(3),
        ExtensionDurationSinceLastInterest = TimeSpan.FromSeconds(1)
    });

    public static void NotificationWarning(string message) => Service.DalamudNotice.AddNotification(new()
    {
        Title = "Daily Routines",
        Content = message,
        Type = NotificationType.Warning,
        InitialDuration = TimeSpan.FromSeconds(3),
        ExtensionDurationSinceLastInterest = TimeSpan.FromSeconds(1)
    });

    public static void NotificationError(string message) => Service.DalamudNotice.AddNotification(new()
    {
        Title = "Daily Routines",
        Content = message,
        Type = NotificationType.Error,
        InitialDuration = TimeSpan.FromSeconds(3),
        ExtensionDurationSinceLastInterest = TimeSpan.FromSeconds(1)
    });

    public static void NotificationInfo(string message) => Service.DalamudNotice.AddNotification(new()
    {
        Title = "Daily Routines",
        Content = message,
        Type = NotificationType.Info,
        InitialDuration = TimeSpan.FromSeconds(3),
        ExtensionDurationSinceLastInterest = TimeSpan.FromSeconds(1)
    });

    public static void ChatError(string message) => 
        Service.Chat.PrintError(new SeStringBuilder().Append(DRPrefix).AddUiForeground($" {message}", 518).Build());

    public static void ChatError(SeString message)
    {
        var builder = new SeStringBuilder();
        builder.Append(DRPrefix).Append(" ");
        foreach (var payload in message.Payloads)
        {
            if (payload.Type == PayloadType.RawText)
                builder.AddUiForeground($" {((TextPayload)payload).Text}", 518);
            else builder.Add(payload);
        }
    }

    public static void Chat(string message) => Service.Chat.Print(new SeStringBuilder().Append(DRPrefix).Append($" {message}").Build());

    public static void Chat(SeString message) => Service.Chat.Print(new SeStringBuilder().Append(DRPrefix).Append(" ").Append(message).Build());

    public static void Debug(string message) => Service.Log.Debug(message);

    public static void Debug(string message, Exception ex) => Service.Log.Debug(ex, message);

    public static void Warning(string message) => Service.Log.Warning(message);

    public static void Warning(string message, Exception ex) => Service.Log.Warning(ex, message);

    public static void Error(string message) => Service.Log.Error(message);

    public static void Error(string message, Exception ex) => Service.Log.Error(ex, message);
}
