using System;
using System.Globalization;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoNotifyLeveUpdateTitle", "AutoNotifyLeveUpdateDescription", ModuleCategories.Notice)]
public class AutoNotifyLeveUpdate : DailyModuleBase
{
    public override string? Author { get; set; } = "HSS";
    private const string LeveSigScanner = "88 05 ?? ?? ?? ?? 0F B7 41 06";
    private static DateTime nextLeveCheck = DateTime.MinValue;
    private static DateTime finishTime = DateTime.Now;
    private static int lastLeve;
    private static bool OnChatMessage;
    private static bool JustFull;

    public override void Init()
    {
        AddConfig(this, "OnChatMessage", true);
        AddConfig(this, "JustFull", true);
        OnChatMessage = GetConfig<bool>(this, "OnChatMessage");
        JustFull = GetConfig<bool>(this, "JustFull");
        Service.Framework.Update += OnFrameworkLeve;
    }

    public override void ConfigUI()
    {
        PreviewImageWithHelpText(Service.Lang.GetText("AutoNotifyLeveUpdate-NotificationMessageHelp"),
                                 "https://mirror.ghproxy.com/https://raw.githubusercontent.com/AtmoOmen/DailyRoutines/main/imgs/AutoNotifyLeveUpdate-1.png");

        ImGui.Spacing();

        ImGui.Text(lastLeve == 100
                       ? Service.Lang.GetText("AutoNotifyLeveUpdate-NotificationFullText")
                       : $"{Service.Lang.GetText("AutoNotifyLeveUpdate-NotificationText")}{finishTime.ToString(CultureInfo.CurrentCulture)}");

        ImGui.Text($"{Service.Lang.GetText("AutoNotifyLeveUpdate-LeveUpdateTimeText")}{nextLeveCheck}");

        if (ImGui.Checkbox(Service.Lang.GetText("AutoNotifyLeveUpdate-OnChatMessageConfig"),
                           ref OnChatMessage))
            UpdateConfig(this, "OnChatMessage", OnChatMessage);
        if (ImGui.Checkbox(Service.Lang.GetText("AutoNotifyLeveUpdate-JustFullConfig"),
                           ref JustFull))
            UpdateConfig(this, "JustFull", JustFull);
    }

    private static void OnFrameworkLeve(IFramework _)
    {
        if (!EzThrottler.Throttle("AutoNotifyLeveUpdate", 5000)) return;
        if (Service.ClientState.LocalPlayer == null)
            return;
        var firstFlag = nextLeveCheck == DateTime.MinValue;
        var tempLastLeve = lastLeve;
        var now = DateTime.Now;
        var leves = Leves(Service.SigScanner.GetStaticAddressFromSig(LeveSigScanner));
        leves = leves < 0 ? 0 : leves > 100 ? 100 : leves;
        lastLeve = leves;
        if (leves == tempLastLeve && tempLastLeve != 100) return;
        var needDay = DaysToReachHundred(leves, now.TimeOfDay);
        finishTime = now.AddDays(needDay);
        if (finishTime.TimeOfDay >= TimeSpan.FromHours(20))
            finishTime = new DateTime(finishTime.Year, finishTime.Month, finishTime.Day, 20, 0, 0);
        else if (finishTime.TimeOfDay >= TimeSpan.FromHours(8))
            finishTime = new DateTime(finishTime.Year, finishTime.Month, finishTime.Day, 8, 0, 0);
        else
            finishTime = new DateTime(finishTime.Year, finishTime.Month, finishTime.Day - 1, 20, 0, 0);
        if (tempLastLeve <= leves)
        {
            if (leves == 100)
            {
                if (OnChatMessage)
                {
                    Service.Chat.Print($"{Service.Lang.GetText("AutoNotifyLeveUpdate-NotificationTitle")}{leves}" +
                                       $"\n {Service.Lang.GetText("AutoNotifyLeveUpdate-NotificationFullText")}" +
                                       $"\n {Service.Lang.GetText("AutoNotifyLeveUpdate-LeveUpdateTimeText")}{nextLeveCheck.ToString(CultureInfo.CurrentCulture)}");
                }

                finishTime = now;
                Service.Notice.Notify($"{Service.Lang.GetText("AutoNotifyLeveUpdate-NotificationTitle")}{leves}",
                                      Service.Lang.GetText("AutoNotifyLeveUpdate-NotificationFullText"));
                return;
            }

            if (JustFull) return;
            if (OnChatMessage)
            {
                Service.Chat.Print(
                    $"\n {Service.Lang.GetText("AutoNotifyLeveUpdate-NotificationText")}{finishTime.ToString(CultureInfo.CurrentCulture)}" +
                    $"\n {Service.Lang.GetText("AutoNotifyLeveUpdate-LeveUpdateTimeText")}{nextLeveCheck.ToString(CultureInfo.CurrentCulture)}");
            }

            if (firstFlag) return;
            Service.Notice.Notify($"{Service.Lang.GetText("AutoNotifyLeveUpdate-NotificationTitle")}{leves}",
                                  $"{Service.Lang.GetText("AutoNotifyLeveUpdate-NotificationText")}\n{finishTime.ToString(CultureInfo.CurrentCulture)}");
        }
    }

    private static unsafe int Leves(IntPtr address)
    {
        if (address != IntPtr.Zero)
            return *(byte*)address;

        return -1;
    }

    private static int DaysToReachHundred(int currentCount, TimeSpan currentTime)
    {
        var now = DateTime.Now;
        const int dailyIncrement = 6;

        var incrementToday = 0;

        if (currentTime >= TimeSpan.FromHours(8))
        {
            nextLeveCheck = new DateTime(now.Year, now.Month, now.Day, 20, 0, 0);
            incrementToday += 3;
        }
        else if (currentTime >= TimeSpan.FromHours(20))
        {
            nextLeveCheck = new DateTime(now.Year, now.Month, now.Day + 1, 8, 0, 0);
            incrementToday += 3;
        }
        else
        {
            nextLeveCheck = new DateTime(now.Year, now.Month, now.Day, 8, 0, 0);
        }


        var remaining = 100 - currentCount - incrementToday;

        if (remaining <= 0)
            return 0;

        var daysNeeded = (int)Math.Ceiling((double)remaining / dailyIncrement);

        return daysNeeded;
    }

    public override void Uninit()
    {
        Service.Framework.Update -= OnFrameworkLeve;
        base.Uninit();
    }
}
