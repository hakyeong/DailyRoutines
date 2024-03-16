using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoNotifyReadyCheckTitle", "AutoNotifyReadyCheckDescription", ModuleCategories.Notice)]
public class AutoNotifyReadyCheck : DailyModuleBase
{
    public override void Init()
    {
        Service.Chat.ChatMessage += OnChatMessage;
    }

    private static void OnChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if (type != XivChatType.SystemMessage && type != (XivChatType)313) return;

        var content = message.ExtractText();
        if (!content.Contains("发起了准备确认")) return;

        content = content.Trim('。');

        Service.Notice.Notify(content, content);
    }

    public override void Uninit()
    {
        Service.Chat.ChatMessage -= OnChatMessage;

        base.Uninit();
    }
}
