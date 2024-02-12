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

    public delegate void FilterSeStringDelegate(nint vulgarInstance, ref Utf8String utf8String);
    [Signature("E8 ?? ?? ?? ?? 48 8B C3 48 83 C4 ?? 5B C3 CC CC CC CC CC CC CC 48 83 EC", DetourName = nameof(FilterSeString))]
    public Hook<FilterSeStringDelegate>? FilterSeStringCheckHook;

    public delegate bool CensorshipCheckDelegate(nint vulgarInstance, Utf8String utf8String);
    [Signature("E8 ?? ?? ?? ?? 84 C0 74 16 48 8D 15 ?? ?? ?? ??", DetourName = nameof(CensorshipCheck))]
    public Hook<CensorshipCheckDelegate>? CensorshipCheckHook;

    public void Init()
    {
        SignatureHelper.Initialise(this);
        FilterSeStringCheckHook?.Enable();
        CensorshipCheckHook?.Enable();
        VulgarInstance = Marshal.ReadIntPtr((nint)Framework.Instance() + 0x2B40);

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, "ChatLog", OnAddonUpdate);
    }

    public void ConfigUI()
    {
        var infoImageState = ThreadLoadImageHandler.TryGetTextureWrap(
            "https://raw.githubusercontent.com/AtmoOmen/DailyRoutines/main/imgs/AutoAntiCensorship-1.png",
            out var imageHandler);

        ImGui.TextColored(ImGuiColors.DalamudOrange, Service.Lang.GetText("AutoAntiCensorship-Preview"));

        ImGui.SameLine();
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.TextDisabled(FontAwesomeIcon.InfoCircle.ToIconString());
        ImGui.PopFont();

        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            if (infoImageState)
                ImGui.Image(imageHandler.ImGuiHandle, new Vector2(443, 82));
            else
                ImGui.TextDisabled($"{Service.Lang.GetText("ImageLoading")}...");
            ImGui.EndTooltip();
        }
    }

    public void OverlayUI() { }

    private void OnAddonUpdate(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)args.Addon;
        var textInput = (AtkComponentTextInput*)addon->GetComponentNodeById(5);
        var text = Marshal.PtrToStringUTF8((nint)textInput->AtkComponentInputBase.UnkText1.StringPtr);
        if (string.IsNullOrWhiteSpace(text)) return;
        if (text.Contains('/')) return;

        var handledText = BypassCensorship(text);
        textInput->AtkComponentInputBase.UnkText1 = *Utf8String.FromString(handledText);
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
                if (processedText[i] == '*')
                {
                    isCensored = true;

                    var start = i;
                    while (i < processedText.Length && processedText[i] == '*') i++;

                    var length = i - start;
                    // 单个汉字
                    if (length == 1 && IsChineseCharacter(text[start]))
                    {
                        var pinyin = PinyinHelper.GetPinyin(text[start].ToString()).ToLower();
                        var screenedPinyin = GetFilteredString(pinyin);
                        tempResult.Append(pinyin != screenedPinyin ? InsertDots(pinyin) : pinyin);
                    }
                    else // 汉字词或英文
                        tempResult.Append(InsertDots(text.Substring(start, length)));
                }
                else
                {
                    tempResult.Append(text[i]);
                    i++;
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

    private string GetFilteredString(string str)
    {
        var utf8String = Utf8String.FromString(str);
        FilterSeStringCheckHook.Original(VulgarInstance, ref *utf8String);
        return (*utf8String).ToString();
    }

    private void FilterSeString(nint vulgarInstance, ref Utf8String utf8String)
    {
        var origString = utf8String.ToString();
        var handledString = BypassCensorship(origString);
        utf8String.SetString(handledString);
        FilterSeStringCheckHook.Original(vulgarInstance, ref utf8String);
    }

    private bool CensorshipCheck(nint vulgarInstance, Utf8String utf8String)
    {
        return false;
    }

    public void Uninit()
    {
        FilterSeStringCheckHook?.Dispose();
        CensorshipCheckHook?.Dispose();
        Service.AddonLifecycle.UnregisterListener(OnAddonUpdate);
    }
}
