using System;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using ImGuiNET;
using System.Numerics;
namespace DailyRoutines.Modules
{
    [ModuleDescription("AutoNotifyCountdownTitle", "AutoNotifyCountdownDescription", ModuleCategories.Notice)]
    public class AutoNotifyCountdown: DailyModuleBase
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
                                     "https://mirror.ghproxy.com/https://raw.githubusercontent.com/AtmoOmen/DailyRoutines/main/imgs/AutoNotifyCountdown.png-1.png",
                                     new Vector2(378, 113));

            if (ImGui.Checkbox(Service.Lang.GetText("OnlyNotifyWhenBackground"),
                               ref ConfigOnlyNotifyWhenBackground))
                UpdateConfig(this, "OnlyNotifyWhenBackground", ConfigOnlyNotifyWhenBackground);
        }

        private static void OnChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled)
        {
            if (ConfigOnlyNotifyWhenBackground && HelpersOm.IsGameForeground()) return;

            var uintType = (uint)type;
            if (uintType != 185) return;
            
            var content = message.ExtractText();
            if (!content.StartsWith("距离战斗开始还有") && !content.StartsWith("Battle commencing in ") && !content.StartsWith("戦闘開始まで")) return;
            
            if (!content.EndsWith('）') && !content.EndsWith(')')) return;
            
            Service.Notice.Notify(Service.Lang.GetText("AutoNotifyCountdown-NotificationTitle"), content);
        }

        public override void Uninit()
        {
            Service.Chat.ChatMessage -= OnChatMessage;

            base.Uninit();
        }
    }
}
