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
                                     "https://mirror.ghproxy.com/https://raw.githubusercontent.com/AtmoOmen/DailyRoutines/main/imgs/AutoNotifyCountdown-1.png");

            if (ImGui.Checkbox(Service.Lang.GetText("OnlyNotifyWhenBackground"),
                               ref ConfigOnlyNotifyWhenBackground))
                UpdateConfig(this, "OnlyNotifyWhenBackground", ConfigOnlyNotifyWhenBackground);
        }

        private static void OnChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled)
        {
            if (ConfigOnlyNotifyWhenBackground && HelpersOm.IsGameForeground()) return;
            var uintType = (uint)type;
            if (uintType != 185) return;

            var result = Service.Data.GetExcelSheet<LogMessage>().GetRow(5255).Text;
            if (result == null) return;
            if (result.Payloads[0].PayloadType != PayloadType.Text) return;
            var startFlag = result.Payloads[0].RawString;

            var content = message.ExtractText();
            if (!content.StartsWith(startFlag) && !content.EndsWith('）') && !content.EndsWith(')')) return;
            WinToast.Notify(Service.Lang.GetText("AutoNotifyCountdown-NotificationTitle"), content);
        }

        public override void Uninit()
        {
            Service.Chat.ChatMessage -= OnChatMessage;

            base.Uninit();
        }
    }
}
