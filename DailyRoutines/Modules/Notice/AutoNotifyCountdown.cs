using System.Linq;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets2;
using System.Numerics;
using PayloadType = Lumina.Text.Payloads.PayloadType;
namespace DailyRoutines.Modules
{
    [ModuleDescription("AutoNotifyCountdownTitle", "AutoNotifyCountdownDescription", ModuleCategories.Notice)]
    public class AutoNotifyCountdown : DailyModuleBase
    {
        public override string? Author { get; set; } = "HSS";
        private static bool ConfigOnlyNotifyWhenBackground;

        public override void Init()
        {
            AddConfig(this, "OnlyNotifyWhenBackground", true);
            ConfigOnlyNotifyWhenBackground = GetConfig<bool>(this, "OnlyNotifyWhenBackground");

            Service.Chat.ChatMessage += OnChatMessage;
        }

        public override void ConfigUI()
        {
            PreviewImageWithHelpText(Service.Lang.GetText("AutoNotifyCountdown-NotificationMessageHelp"),
                                     "https://gh.atmoomen.top/DailyRoutines/main/imgs/AutoNotifyCountdown-1.png");

            if (ImGui.Checkbox(Service.Lang.GetText("OnlyNotifyWhenBackground"),
                               ref ConfigOnlyNotifyWhenBackground))
                UpdateConfig(this, "OnlyNotifyWhenBackground", ConfigOnlyNotifyWhenBackground);
        }

        private static void OnChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled)
        {
            if (ConfigOnlyNotifyWhenBackground && HelpersOm.IsGameForeground()) return;
            var uintType = (uint)type;
            if (uintType != 185) return;
            
            var msg = message.TextValue;
            if (Service.PayloadText.Countdown.All(s => msg.Contains(msg)))
            {
                WinToast.Notify(Service.Lang.GetText("AutoNotifyCountdown-NotificationTitle"), message.ExtractText());
            }
        }

        public override void Uninit()
        {
            Service.Chat.ChatMessage -= OnChatMessage;

            base.Uninit();
        }
    }
}
