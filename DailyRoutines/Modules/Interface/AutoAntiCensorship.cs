using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using DailyRoutines.Helpers;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Hooking;
using Dalamud.Memory;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;
using TinyPinyin;

// ReSharper disable All

namespace DailyRoutines.Modules;

[ModuleDescription("AutoAntiCensorshipTitle", "AutoAntiCensorshipDescription", ModuleCategories.Interface)]
public unsafe class AutoAntiCensorship : DailyModuleBase
{
    private enum State
    {
        OutsideTag,
        InsideTag
    }

    [Signature("E8 ?? ?? ?? ?? 48 8B C3 48 83 C4 ?? 5B C3 CC CC CC CC CC CC CC 48 83 EC")]
    private readonly delegate* unmanaged <nint, Utf8String*, void> GetFilteredUtf8String;

    [Signature(
        "E8 ?? ?? ?? ?? 48 8B 0D ?? ?? ?? ?? 48 8B 89 ?? ?? ?? ?? 48 85 C9 74 ?? 48 8B D3 E8 ?? ?? ?? ?? 48 8B C3")]
    private readonly delegate* unmanaged <long, long, long> LocalMessageDisplayHandler;

    [Signature("E8 ?? ?? ?? ?? 48 8B 0D ?? ?? ?? ?? 48 8B 81 ?? ?? ?? ?? 48 85 C0 74 ?? 48 8B D3")]
    private readonly delegate* unmanaged <long, long, long> PartyFinderMessageDisplayHandler;

    private delegate long LocalMessageDelegate(long a1, long a2);

    [Signature("40 53 48 83 EC ?? 48 8D 99 ?? ?? ?? ?? 48 8B CB E8 ?? ?? ?? ?? 48 8B 0D",
               DetourName = nameof(LocalMessageDetour))]
    private Hook<LocalMessageDelegate>? LocalMessageDisplayHook;

    private delegate byte ProcessSendedChatDelegate(nint uiModule, byte** message, nint a3);

    [Signature("E8 ?? ?? ?? ?? FE 86 ?? ?? ?? ?? C7 86 ?? ?? ?? ?? ?? ?? ?? ??",
               DetourName = nameof(ProcessSendedChatDetour))]
    private static Hook<ProcessSendedChatDelegate>? ProcessSendedChatHook;

    private delegate long PartyFinderMessageDisplayDelegate(long a1, long a2);

    [Signature("48 89 5C 24 ?? 57 48 83 EC ?? 48 8D 99 ?? ?? ?? ?? 48 8B F9 48 8B CB E8",
               DetourName = nameof(PartyFinderMessageDisplayDetour))]
    private static Hook<PartyFinderMessageDisplayDelegate>? PartyFinderMessageDisplayHook;

    private delegate char LookingForGroupConditionReceiveEventDelegate(long a1, long a2);

    [Signature(
        "48 89 5C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 8D AC 24 ?? ?? ?? ?? B8 ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 2B E0 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 85 ?? ?? ?? ?? 48 8B D9",
        DetourName = nameof(LookingForGroupConditionReceiveEventDetour))]
    private static Hook<LookingForGroupConditionReceiveEventDelegate>? LookingForGroupConditionReceiveEventHook;

    private static readonly char[] SpecialChars =
        ['*', '%', '+', '-', '^', '=', '|', '&', '!', '~', ',', '.', ';', ':', '?', '@', '#'];

    private static Dictionary<char, char>? SpecialCharTranslateDictionary;
    private static Dictionary<char, char>? SpecialCharTranslateDictionaryRe;

    private string PreviewInput = string.Empty;
    private string PreviewCensorship = string.Empty;
    private string PreviewBypass = string.Empty;

    public override void Init()
    {
        if (SpecialCharTranslateDictionary == null)
        {
            SpecialCharTranslateDictionary = [];
            for (var i = 0; i < SpecialChars.Length; i++)
            {
                var specialChar = SpecialChars[i];
                SpecialCharTranslateDictionary[specialChar] = (char)i;
            }

            SpecialCharTranslateDictionaryRe = SpecialCharTranslateDictionary.ToDictionary(x => x.Value, x => x.Key);
        }

        Service.Hook.InitializeFromAttributes(this);

        LocalMessageDisplayHook?.Enable();
        ProcessSendedChatHook?.Enable();

        PartyFinderMessageDisplayHook?.Enable();
        LookingForGroupConditionReceiveEventHook?.Enable();
    }

    public override void ConfigUI()
    {
        PreviewImageWithHelpText(Service.Lang.GetText("AutoAntiCensorship-Preview"),
                                 "https://gh.atmoomen.top/DailyRoutines/main/Assets/Images/AutoAntiCensorship-1.png");

        ImGui.SameLine();
        PreviewImageWithHelpText(Service.Lang.GetText("AutoAntiCensorship-Preview1"),
                                 "https://gh.atmoomen.top/DailyRoutines/main/Assets/Images/AutoAntiCensorship-2.png");

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

    private char LookingForGroupConditionReceiveEventDetour(long a1, long a2)
    {
        var eventType = AddonHelper.GetAtkValueInt((nint)a2);
        if (eventType != 15) return LookingForGroupConditionReceiveEventHook.Original(a1, a2);

        var originalText = Marshal.PtrToStringUTF8((nint)AddonHelper.GetAtkValueString((nint)a2 + 16));
        var handledText = BypassCensorship(originalText);
        if (handledText != originalText)
        {
            AgentHelper.SendEvent(AgentId.LookingForGroup, 3, 15, handledText, 0);
            return (char)0;
        }

        return LookingForGroupConditionReceiveEventHook.Original(a1, a2);
    }

    // 消息发送处理
    private byte ProcessSendedChatDetour(nint uiModule, byte** message, nint a3)
    {
        var originalSeString = MemoryHelper.ReadSeStringNullTerminated((nint)(*message));
        var messageDecode = originalSeString.ToString();

        if (string.IsNullOrWhiteSpace(messageDecode) || messageDecode.StartsWith('/'))
            return ProcessSendedChatHook.Original(uiModule, message, a3);

        var ssb = new SeStringBuilder();
        foreach (var payload in originalSeString.Payloads)
        {
            if (payload.Type == PayloadType.RawText)
                ssb.Append(BypassCensorship(((TextPayload)payload).Text));
            else
                ssb.Add(payload);
        }

        var filteredSeString = ssb.Build();

        if (filteredSeString.TextValue.Length <= 500)
        {
            var utf8String = Utf8String.FromString(".");
            utf8String->SetString(filteredSeString.Encode());

            return ProcessSendedChatHook.Original(uiModule, (byte**)((nint)utf8String).ToPointer(), a3);
        }

        return ProcessSendedChatHook.Original(uiModule, message, a3);
    }

    // 本地聊天消息显示处理函数
    private long LocalMessageDetour(long a1, long a2) => LocalMessageDisplayHandler(a1 + 1096, a2);

    // 招募板信息显示处理函数
    private long PartyFinderMessageDisplayDetour(long a1, long a2) => PartyFinderMessageDisplayHandler(a1 + 10488, a2);

    // 获取屏蔽词处理后的文本
    private string GetFilteredString(string str)
    {
        var utf8String = Utf8String.FromString(str);
        GetFilteredUtf8String(Marshal.ReadIntPtr((nint)Framework.Instance() + 0x2B40), utf8String);

        return (*utf8String).ToString();
    }

    private string BypassCensorship(string text)
    {
        text = ReplaceSpecialChars(text, false);

        StringBuilder tempResult = new(text.Length);
        var isCensored = true;

        while (isCensored)
        {
            isCensored = false;
            var processedText = GetFilteredString(text);
            tempResult.Clear();


            var state = State.OutsideTag;
            for (var i = 0; i < processedText.Length;)
            {
                switch (state)
                {
                    case State.OutsideTag:
                        if (processedText[i] == '<')
                        {
                            state = State.InsideTag;
                            var end = processedText.IndexOf('>', i);
                            if (end != -1)
                            {
                                tempResult.Append(text.AsSpan(i, end - i + 1));
                                i = end + 1;
                            }
                            else
                            {
                                tempResult.Append(text.AsSpan(i));
                                i = processedText.Length;
                            }
                        }
                        else if (processedText[i] == '*')
                        {
                            isCensored = true;
                            var start = i;
                            while (++i < processedText.Length && processedText[i] == '*') ;

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

                        break;

                    case State.InsideTag:
                        var endTag = processedText.IndexOf('>', i);
                        if (endTag != -1)
                        {
                            tempResult.Append(text.AsSpan(i, endTag - i + 1));
                            i = endTag + 1;
                            state = State.OutsideTag;
                        }
                        else
                        {
                            tempResult.Append(text.AsSpan(i));
                            i = processedText.Length;
                            state = State.OutsideTag;
                        }

                        break;
                }
            }

            text = tempResult.ToString();
        }

        return ReplaceSpecialChars(text, true);
    }


    public static string ReplaceSpecialChars(string input, bool IsReversed)
    {
        if (string.IsNullOrEmpty(input)) return input;

        var dic = IsReversed ? SpecialCharTranslateDictionaryRe : SpecialCharTranslateDictionary;

        var sb = new StringBuilder(input.Length);
        foreach (var c in input)
        {
            if (dic.TryGetValue(c, out var replacement))
                sb.Append(replacement);
            else
                sb.Append(c);
        }

        return sb.ToString();
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
}
