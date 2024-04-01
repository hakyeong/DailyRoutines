using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using TinyPinyin;
// ReSharper disable All

namespace DailyRoutines.Modules;

[ModuleDescription("AutoAntiCensorshipTitle", "AutoAntiCensorshipDescription", ModuleCategories.Interface)]
public unsafe class AutoAntiCensorship : DailyModuleBase
{
    private nint VulgarInstance = nint.Zero;

    [Signature("E8 ?? ?? ?? ?? 48 8B C3 48 83 C4 ?? 5B C3 CC CC CC CC CC CC CC 48 83 EC")]
    public readonly delegate* unmanaged <nint, Utf8String*, void> GetFilteredUtf8String;

    [Signature(
        "E8 ?? ?? ?? ?? 48 8B 0D ?? ?? ?? ?? 48 8B 89 ?? ?? ?? ?? 48 85 C9 74 ?? 48 8B D3 E8 ?? ?? ?? ?? 48 8B C3")]
    public readonly delegate* unmanaged <long, long, long> LocalMessageDisplayHandler;

    [Signature("E8 ?? ?? ?? ?? 48 8B 0D ?? ?? ?? ?? 48 8B 81 ?? ?? ?? ?? 48 85 C0 74 ?? 48 8B D3")]
    public readonly delegate* unmanaged <long, long, long> PartyFinderMessageDisplayHandler;

    public delegate long PartyFinderMessageDisplayDelegate(long a1, long a2);

    [Signature("48 89 5C 24 ?? 57 48 83 EC ?? 48 8D 99 ?? ?? ?? ?? 48 8B F9 48 8B CB E8",
               DetourName = nameof(PartyFinderMessageDisplayDetour))]
    public Hook<PartyFinderMessageDisplayDelegate>? PartyFinderMessageDisplayHook;

    public delegate long LocalMessageDelegate(long a1, long a2);

    [Signature("40 53 48 83 EC ?? 48 8D 99 ?? ?? ?? ?? 48 8B CB E8 ?? ?? ?? ?? 48 8B 0D",
               DetourName = nameof(LocalMessageDetour))]
    public Hook<LocalMessageDelegate>? LocalMessageDisplayHook;

    private delegate nint AddonReceiveEventDelegate(
        AtkEventListener* self, AtkEventType eventType, uint eventParam, AtkEvent* eventData, ulong* inputData);

    private Hook<AddonReceiveEventDelegate>? ChatLogTextInputHook;
    private Hook<AddonReceiveEventDelegate>? PartyFinderDescriptionInputHook;

    private const string AutoTranslateLeft = "\u0002\u0012\u00027\u0003";
    private const string AutoTranslateRight = "\u0002\u0012\u00028\u0003";

    private string PreviewInput = string.Empty;
    private string PreviewCensorship = string.Empty;
    private string PreviewBypass = string.Empty;

    public override void Init()
    {
        Service.Hook.InitializeFromAttributes(this);

        PartyFinderMessageDisplayHook?.Enable();
        LocalMessageDisplayHook?.Enable();
        VulgarInstance = Marshal.ReadIntPtr((nint)Framework.Instance() + 0x2B40);

        if (Service.Gui.GetAddonByName("ChatLog") != nint.Zero)
            OnChatLogAddonSetup(AddonEvent.PostSetup, null);
        else
            Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "ChatLog", OnChatLogAddonSetup);

        if (Service.Gui.GetAddonByName("LookingForGroupCondition") != nint.Zero)
            OnPartyFinderConditionAddonSetup(AddonEvent.PostSetup, null);
        else
            Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "LookingForGroupCondition",
                                                    OnChatLogAddonSetup);
    }

    public override void ConfigUI()
    {
        PreviewImageWithHelpText(Service.Lang.GetText("AutoAntiCensorship-Preview"),
                                 "https://raw.githubusercontent.com/AtmoOmen/DailyRoutines/main/imgs/AutoAntiCensorship-1.png",
                                 new Vector2(386, 105));

        ImGui.SameLine();
        PreviewImageWithHelpText(Service.Lang.GetText("AutoAntiCensorship-Preview1"),
                                 "https://raw.githubusercontent.com/AtmoOmen/DailyRoutines/main/imgs/AutoAntiCensorship-2.png",
                                 new Vector2(383, 36));

        ImGui.Separator();

        ImGui.Spacing();

        ImGui.AlignTextToFramePadding();
        ImGui.Text($"{Service.Lang.GetText("AutoAntiCensorship-OrigText")}:");

        ImGui.SameLine();
        if (ImGui.InputText("###PreviewInput", ref PreviewInput, 1000,
                            ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.AutoSelectAll))
        {
            PreviewCensorship = GetFilteredString(PreviewInput);
            PreviewBypass = BypassCensorship(PreviewInput);
        }

        if (!string.IsNullOrWhiteSpace(PreviewInput))
        {
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.PushTextWrapPos(ImGui.GetFontSize() * 20f);
                ImGui.TextUnformatted(PreviewInput);
                ImGui.PopTextWrapPos();
                ImGui.EndTooltip();
            }
        }

        ImGui.AlignTextToFramePadding();
        ImGui.Text($"{Service.Lang.GetText("AutoAntiCensorship-FilteredText")}:");

        ImGui.SameLine();
        ImGui.InputText("###PreviewCensorship", ref PreviewCensorship, 500, ImGuiInputTextFlags.ReadOnly);

        ImGui.AlignTextToFramePadding();
        ImGui.Text($"{Service.Lang.GetText("AutoAntiCensorship-BypassText")}:");

        ImGui.SameLine();
        ImGui.InputText("###PreviewBypass", ref PreviewBypass, 1000, ImGuiInputTextFlags.ReadOnly);
    }

    private void OnChatLogAddonSetup(AddonEvent type, AddonArgs? args)
    {
        var addon = (AtkUnitBase*)Service.Gui.GetAddonByName("ChatLog");
        var address = (nint)addon->GetNodeById(5)->GetComponent()->AtkEventListener.vfunc[2];
        ChatLogTextInputHook ??=
            Service.Hook.HookFromAddress<AddonReceiveEventDelegate>(address, ChatLogTextInputDetour);
        ChatLogTextInputHook?.Enable();
    }

    private void OnPartyFinderConditionAddonSetup(AddonEvent type, AddonArgs? args)
    {
        var addon = (AtkUnitBase*)Service.Gui.GetAddonByName("LookingForGroupCondition");
        var address = (nint)addon->GetNodeById(22)->GetComponent()->AtkEventListener.vfunc[2];
        PartyFinderDescriptionInputHook ??=
            Service.Hook.HookFromAddress<AddonReceiveEventDelegate>(address, PartyFinderDescriptionInputDetour);
        if (!PartyFinderDescriptionInputHook.IsEnabled) PartyFinderDescriptionInputHook?.Enable();
    }

    // 聊天框 Event 处理, 应该找聊天框 SendChat 相关方法的, 但以后再找吧
    private nint ChatLogTextInputDetour(
        AtkEventListener* self, AtkEventType eventType, uint eventParam, AtkEvent* eventData, ulong* inputData)
    {
        if (eventType == AtkEventType.InputReceived)
        {
            var addon = (AtkUnitBase*)Service.Gui.GetAddonByName("ChatLog");
            var textInput = (AtkComponentTextInput*)addon->GetComponentNodeById(5);

            var text1 = Marshal.PtrToStringUTF8((nint)textInput->AtkComponentInputBase.UnkText1.StringPtr);
            if (string.IsNullOrWhiteSpace(text1) || text1.StartsWith('/'))
                return ChatLogTextInputHook.Original(self, eventType, eventParam, eventData, inputData);

            // UnkText1 暂时没看出来是怎么判断处理定型文的, 搁置了
            var text2 = Marshal.PtrToStringUTF8((nint)textInput->AtkComponentInputBase.UnkText2.StringPtr);
            if (text2.Contains(AutoTranslateLeft))
                return ChatLogTextInputHook.Original(self, eventType, eventParam, eventData, inputData);

            var handledText = BypassCensorship(text1);
            textInput->AtkComponentInputBase.UnkText1 = *Utf8String.FromString(handledText);
            textInput->AtkComponentInputBase.UnkText2 = *Utf8String.FromString(handledText);
        }

        return ChatLogTextInputHook.Original(self, eventType, eventParam, eventData, inputData);
    }

    // 招募板描述 Event 处理
    private nint PartyFinderDescriptionInputDetour(
        AtkEventListener* self, AtkEventType eventType, uint eventParam, AtkEvent* eventData, ulong* inputData)
    {
        if (eventType is AtkEventType.InputReceived or AtkEventType.FocusStop or AtkEventType.MouseClick)
        {
            var addon = (AtkUnitBase*)Service.Gui.GetAddonByName("LookingForGroupCondition");
            var textInput = (AtkComponentTextInput*)addon->GetComponentNodeById(22);

            var text1 = Marshal.PtrToStringUTF8((nint)textInput->AtkComponentInputBase.UnkText1.StringPtr);
            if (string.IsNullOrWhiteSpace(text1) || text1.StartsWith('/'))
                return PartyFinderDescriptionInputHook.Original(self, eventType, eventParam, eventData, inputData);

            var text2 = Marshal.PtrToStringUTF8((nint)textInput->AtkComponentInputBase.UnkText2.StringPtr);
            if (text2.Contains(AutoTranslateLeft))
                return PartyFinderDescriptionInputHook.Original(self, eventType, eventParam, eventData, inputData);

            var handledText = BypassCensorship(text1);
            textInput->AtkComponentInputBase.UnkText1 = *Utf8String.FromString(handledText);
            textInput->AtkComponentInputBase.UnkText2 = *Utf8String.FromString(handledText);
        }

        return PartyFinderDescriptionInputHook.Original(self, eventType, eventParam, eventData, inputData);
    }

    // 本地聊天消息显示处理函数
    private long LocalMessageDetour(long a1, long a2)
    {
        return LocalMessageDisplayHandler(a1 + 1096, a2);
    }

    // 招募板信息显示处理函数
    private long PartyFinderMessageDisplayDetour(long a1, long a2)
    {
        return PartyFinderMessageDisplayHandler(a1 + 10488, a2);
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
        const string placeholder = "\u0001";
        text = text.Replace("*", placeholder);

        StringBuilder tempResult = new(text.Length);
        bool isCensored;
        do
        {
            isCensored = false;
            var processedText = GetFilteredString(text);
            tempResult.Clear();

            for (var i = 0; i < processedText.Length; )
            {
                if (processedText[i] == '<')
                {
                    var end = processedText.IndexOf('>', i + 1);
                    if (end != -1)
                    {
                        tempResult.Append(text.AsSpan(i, end - i + 1));
                        i = end + 1;
                        continue;
                    }
                }
                else if (processedText[i] == '*')
                {
                    isCensored = true;
                    var start = i;
                    while (++i < processedText.Length && processedText[i] == '*');

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
                    tempResult.Append(processedText[i++]);
                }
            }

            text = tempResult.ToString();
        } while (isCensored);

        return text.Replace(placeholder, "*");
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

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnChatLogAddonSetup);

        base.Uninit();
    }
}
