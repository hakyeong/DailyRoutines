using System;
using System.Globalization;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using DailyRoutines.Notifications;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoNotifyLeveUpdateTitle", "AutoNotifyLeveUpdateDescription", ModuleCategories.Notice)]
public unsafe class AutoNotifyLeveUpdate : DailyModuleBase
{
    public override string? Author { get; set; } = "HSS";
    private static DateTime nextLeveCheck = DateTime.MinValue;
    private static DateTime finishTime = DateTime.UtcNow;
    private static int lastLeve = 0;
    private static bool OnChatMessage;
    private static int NotificationThreshold;

    public override void Init()
    {
        AddConfig("OnChatMessage", true);
        AddConfig("NotificationThreshold", 97);
        OnChatMessage = GetConfig<bool>("OnChatMessage");
        NotificationThreshold = GetConfig<int>("NotificationThreshold");
        Service.FrameworkManager.Register(OnFrameworkLeve);
    }

    public override void ConfigUI()
    {
        PreviewImageWithHelpText(Service.Lang.GetText("AutoNotifyLeveUpdate-NotificationMessageHelp"),
                                 "https://gh.atmoomen.top/DailyRoutines/main/Assets/Images/AutoNotifyLeveUpdate-1.png");

        ImGui.Spacing();
        ImGui.Text($"{Service.Lang.GetText("AutoNotifyLeveUpdate-NumText")}{lastLeve}");
        ImGui.Text($"{Service.Lang.GetText("AutoNotifyLeveUpdate-FullTimeText")}{finishTime.ToLocalTime().ToString(CultureInfo.CurrentCulture)}");
        ImGui.Text($"{Service.Lang.GetText("AutoNotifyLeveUpdate-UpdateTimeText")}{nextLeveCheck.ToLocalTime().ToString(CultureInfo.CurrentCulture)}");

        if (ImGui.Checkbox(Service.Lang.GetText("AutoNotifyLeveUpdate-OnChatMessageConfig"),
                           ref OnChatMessage))
            UpdateConfig("OnChatMessage", OnChatMessage);

        ImGui.PushItemWidth(300f);
        ImGui.SliderInt(Service.Lang.GetText("AutoNotifyLeveUpdate-NotificationThreshold"), ref NotificationThreshold, 1, 100);
        ImGui.PopItemWidth();
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            lastLeve = 0;
            UpdateConfig("NotificationThreshold", NotificationThreshold);
        }
        
    }

    private static void OnFrameworkLeve(IFramework _)
    {
        if (!EzThrottler.Throttle("AutoNotifyLeveUpdate", 5000)) return;
        if (Service.ClientState.LocalPlayer == null || !Service.ClientState.IsLoggedIn)
            return;
        var NowUtc = DateTime.UtcNow;
        var leveAllowances = QuestManager.Instance()->NumLeveAllowances;
        if (!lastLeve.Equals(leveAllowances))
        {
            var decreasing = leveAllowances > lastLeve;
            lastLeve = QuestManager.Instance()->NumLeveAllowances;
            nextLeveCheck = MathNextTime(NowUtc);
            finishTime = MathFinishTime(leveAllowances, NowUtc);
            
            if (leveAllowances >= NotificationThreshold && decreasing)
            {
                if (OnChatMessage)
                {
                    Service.Chat.Print($"{Service.Lang.GetText("AutoNotifyLeveUpdate-NotificationTitle")}" +
                                       $"\n {Service.Lang.GetText("AutoNotifyLeveUpdate-NumText")}{leveAllowances}" +
                                       $"\n {Service.Lang.GetText("AutoNotifyLeveUpdate-FullTimeText")}{finishTime.ToLocalTime().ToString(CultureInfo.CurrentCulture)}" +
                                       $"\n {Service.Lang.GetText("AutoNotifyLeveUpdate-UpdateTimeText")}{nextLeveCheck.ToLocalTime().ToString(CultureInfo.CurrentCulture)}");
                }

                WinToast.Notify($"{Service.Lang.GetText("AutoNotifyLeveUpdate-NotificationTitle")}",
                                      $"{Service.Lang.GetText("AutoNotifyLeveUpdate-NumText")}{leveAllowances}" +
                                      $"\n {Service.Lang.GetText("AutoNotifyLeveUpdate-FullTimeText")}{finishTime.ToLocalTime().ToString(CultureInfo.CurrentCulture)}" +
                                      $"\n {Service.Lang.GetText("AutoNotifyLeveUpdate-UpdateTimeText")}{nextLeveCheck.ToLocalTime().ToString(CultureInfo.CurrentCulture)}");
            }
        }
    }

    private static DateTime MathNextTime(DateTime NowUtc)
    {
        if (NowUtc.Hour >= 12)
        {
            return new DateTime(NowUtc.Year, NowUtc.Month, NowUtc.Day + 1, 0, 0, 0, DateTimeKind.Utc);
        }
        else
        {
            return new DateTime(NowUtc.Year, NowUtc.Month, NowUtc.Day, 12, 0, 0, DateTimeKind.Utc);
        }
    }


    private static DateTime MathFinishTime(int num, DateTime NowUtc)
    {
        if (num >= 100)
        {
            return NowUtc;
        }
        var requiredIncrements = 100 - num;
        var requiredPeriods = requiredIncrements / 3;
        if (requiredIncrements % 3 > 0)
        {
            requiredPeriods++;
        }
        var lastIncrementTimeUtc = new DateTime(NowUtc.Year, NowUtc.Month, NowUtc.Day, NowUtc.Hour >= 12 ? 12 : 0, 0, 0, DateTimeKind.Utc);
        return lastIncrementTimeUtc.AddHours(12 * requiredPeriods);
    }
}
