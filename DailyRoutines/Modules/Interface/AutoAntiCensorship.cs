using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.AddonLifecycle;
using Dalamud.Hooking;
using Dalamud.Interface.Colors;
using Dalamud.Interface;
using Dalamud.Utility.Signatures;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using TinyPinyin;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoAntiCensorshipTitle", "AutoAntiCensorshipDescription", ModuleCategories.Interface)]
public unsafe class AutoAntiCensorship : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithConfigUI => true;

    private nint VulgarInstance = nint.Zero;

    [Signature("E8 ?? ?? ?? ?? 48 8B C3 48 83 C4 ?? 5B C3 CC CC CC CC CC CC CC 48 83 EC")]
    public readonly delegate* unmanaged <nint, Utf8String*, void> GetFilteredUtf8String;

    [Signature("E8 ?? ?? ?? ?? 48 8B 0D ?? ?? ?? ?? 48 8B 89 ?? ?? ?? ?? 48 85 C9 74 ?? 48 8B D3 E8 ?? ?? ?? ?? 48 8B C3")]
    public readonly delegate* unmanaged <long, long, long> LocalMessageDisplayHandler;

    public delegate bool CensorshipCheckDelegate(nint vulgarInstance, Utf8String utf8String);
    [Signature("E8 ?? ?? ?? ?? 84 C0 74 16 48 8D 15 ?? ?? ?? ??", DetourName = nameof(PartyFinderCensorshipCheck))]
    public Hook<CensorshipCheckDelegate>? PartyFinderCensorshipCheckHook;

    public delegate long LocalMessageDelegate(long a1, long a2);
    [Signature("40 53 48 83 EC ?? 48 8D 99 ?? ?? ?? ?? 48 8B CB E8 ?? ?? ?? ?? 48 8B 0D", DetourName = nameof(LocalMessageDetour))]
    public Hook<LocalMessageDelegate>? LocalMessageDisplayHook;

    private delegate nint AddonReceiveEventDelegate(AtkEventListener* self, AtkEventType eventType, uint eventParam, AtkEvent* eventData, ulong* inputData);
    private Hook<AddonReceiveEventDelegate>? ChatLogTextInputHook;

    private const string AutoTranslateLeft = "\u0002\u0012\u00027\u0003";
    private const string AutoTranslateRight = "\u0002\u0012\u00028\u0003";

    public void Init()
    {
        SignatureHelper.Initialise(this);

        PartyFinderCensorshipCheckHook?.Enable();
        LocalMessageDisplayHook?.Enable();
        VulgarInstance = Marshal.ReadIntPtr((nint)Framework.Instance() + 0x2B40);

        if (Service.Gui.GetAddonByName("ChatLog") != nint.Zero)
        {
            OnAddonSetup(AddonEvent.PostSetup, null);
            return;
        }
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "ChatLog", OnAddonSetup);
    }
    
    public void ConfigUI()
    {
        PreviewImageWithHelpText(Service.Lang.GetText("AutoAntiCensorship-Preview"),
                                 "https://raw.githubusercontent.com/AtmoOmen/DailyRoutines/main/imgs/AutoAntiCensorship-1.png",
                                 new Vector2(386, 105));

        PreviewImageWithHelpText(Service.Lang.GetText("AutoAntiCensorship-Preview1"),
                                 "https://raw.githubusercontent.com/AtmoOmen/DailyRoutines/main/imgs/AutoAntiCensorship-2.png",
                                 new Vector2(383, 36));
    }

    public void OverlayUI() { }

    private void OnAddonSetup(AddonEvent type, AddonArgs? args)
    {
        var addon = (AtkUnitBase*)Service.Gui.GetAddonByName("ChatLog");
        var address = (nint)addon->GetNodeById(5)->GetComponent()->AtkEventListener.vfunc[2];
        ChatLogTextInputHook = Hook<AddonReceiveEventDelegate>.FromAddress(address, ChatLogTextInputDetour);
        ChatLogTextInputHook.Enable();
    }

    // 聊天框 Event 处理, 应该找聊天框 SendChat 相关方法的, 但以后再找吧
    private nint ChatLogTextInputDetour(AtkEventListener* self, AtkEventType eventType, uint eventParam, AtkEvent* eventData, ulong* inputData)
    {
        if (eventType == AtkEventType.InputReceived)
        {
            var addon = (AtkUnitBase*)Service.Gui.GetAddonByName("ChatLog");
            var textInput = (AtkComponentTextInput*)addon->GetComponentNodeById(5);

            var text1 = Marshal.PtrToStringUTF8((nint)textInput->AtkComponentInputBase.UnkText1.StringPtr);
            if (string.IsNullOrWhiteSpace(text1) || text1.StartsWith('/')) return ChatLogTextInputHook.Original(self, eventType, eventParam, eventData, inputData);

            // UnkText1 暂时没看出来是怎么判断处理定型文的, 搁置了
            var text2 = Marshal.PtrToStringUTF8((nint)textInput->AtkComponentInputBase.UnkText2.StringPtr);
            if (text2.Contains(AutoTranslateLeft)) return ChatLogTextInputHook.Original(self, eventType, eventParam, eventData, inputData);

            var handledText = BypassCensorship(text1);
            textInput->AtkComponentInputBase.UnkText1 = *Utf8String.FromString(handledText);
            textInput->AtkComponentInputBase.UnkText2 = *Utf8String.FromString(handledText);
        }

        return ChatLogTextInputHook.Original(self, eventType, eventParam, eventData, inputData);
    }

    // 原本是本地聊天消息显示处理函数, 现跳过屏蔽词处理方法, 直接调用了游戏内文本处理函数然后返回其结果
    private long LocalMessageDetour(long a1, long a2)
    {
        return LocalMessageDisplayHandler(a1 + 1096, a2);
    }

    // 一律返回 false => 当前文本不存在屏蔽词
    private bool PartyFinderCensorshipCheck(nint vulgarInstance, Utf8String utf8String)
    {
        return false;
    }

    // 获取屏蔽词处理后的文本
    private string GetFilteredString(string str)
    {
        var utf8String = Utf8String.FromString(str);
        GetFilteredUtf8String(VulgarInstance, utf8String);

        return (*utf8String).ToString();
    }

    private string BypassCensorship(string text)
    {
        var tempResult = new StringBuilder(text.Length);
        bool isCensored;
        do
        {
            isCensored = false;
            var processedText = GetFilteredString(text);
            tempResult.Clear();

            var i = 0;
            while (i < processedText.Length)
            {
                if (processedText[i] == '*')
                {
                    isCensored = true;

                    var start = i;
                    while (i < processedText.Length && processedText[i] == '*') i++;

                    var length = i - start;
                    if (length == 1 && IsChineseCharacter(text[start]))
                    {
                        var pinyin = PinyinHelper.GetPinyin(text[start].ToString()).ToLower();
                        var filteredPinyin = GetFilteredString(pinyin);
                        tempResult.Append(pinyin != filteredPinyin ? InsertDots(pinyin) : pinyin);
                    }
                    else
                    {
                        for (var j = 0; j < length; j++)
                        {
                            tempResult.Append(text[start + j]);
                            if (j < length - 1) tempResult.Append('.');
                        }
                    }
                }
                else
                {
                    tempResult.Append(text[i]);
                    i++;
                }
            }

            text = tempResult.ToString();
        }
        while (isCensored);

        return text;
    }

    private static string InsertDots(string input)
    {
        if (input.Length <= 1) return input;

        var result = new StringBuilder(input.Length * 2);
        for (var i = 0; i < input.Length; i++)
        {
            result.Append(input[i]);
            if (i < input.Length - 1) result.Append('.');
        }

        return result.ToString();
    }

    private static bool IsChineseCharacter(char c)
    {
        return c >= 0x4E00 && c <= 0x9FA5;
    }

    public void Uninit()
    {
        ChatLogTextInputHook?.Dispose();
        LocalMessageDisplayHook?.Dispose();
        PartyFinderCensorshipCheckHook?.Dispose();
        Service.AddonLifecycle.UnregisterListener(OnAddonSetup);
    }
}
