using System;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoNotifyRecruitmentEndTitle", "AutoNotifyRecruitmentEndDescription", ModuleCategories.Notice)]
public class AutoNotifyRecruitmentEnd : DailyModuleBase
{
    public override void Init()
    {
        Service.Chat.ChatMessage += OnChatMessage;
    }

    private static void OnChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if (type != XivChatType.SystemMessage) return;

        var content = message.ExtractText();
        if (!content.StartsWith("招募队员结束")) return;
        var parts = content.Split(["，"], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 1)
        {
            Service.Notice.Notify(parts[0], parts[1].Trim('。'));
            return;
        }

        Service.Notice.Notify(parts[0], parts[0]);
    }

    public override void Uninit()
    {
        Service.Chat.ChatMessage -= OnChatMessage;

        base.Uninit();
    }
}
