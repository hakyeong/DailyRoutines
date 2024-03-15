using System.Collections.Generic;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("CustomizeASTCardNamesTitle", "CustomizeASTCardNamesDescription", ModuleCategories.Interface)]
public class CustomizeASTCardNames : DailyModuleBase
{
    private static Dictionary<string, string> CardNames = new()
    {
        { "太阳神之衡", "近战卡" },
        { "放浪神之箭", "近战卡" },
        { "战争神之枪", "近战卡" },
        { "世界树之干", "远程卡" },
        { "河流神之瓶", "远程卡" },
        { "建筑神之塔", "远程卡" }
    };

    public override void Init()
    {
        Service.Config.AddConfig(this, "CardNames", CardNames);
        CardNames = Service.Config.GetConfig<Dictionary<string, string>>(this, "CardNames");

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "JobHudAST0", OnAddon0);
    }

    public override void ConfigUI()
    {
        foreach (var card in CardNames)
        {
            var cardName = card.Key;
            var cardReplacedName = card.Value;

            ImGui.PushID(card.Key);

            ImGui.SetNextItemWidth(70f * ImGuiHelpers.GlobalScale);
            ImGui.InputText("###OriginalNamePreview", ref cardName, 32, ImGuiInputTextFlags.ReadOnly);

            ImGui.SameLine();
            ImGui.Text("——>");

            ImGui.SameLine();
            ImGui.SetNextItemWidth(90f * ImGuiHelpers.GlobalScale);
            if (ImGui.InputText("###ReplaceNameInput", ref cardReplacedName, 32, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                CardNames[cardName] = cardReplacedName;
                Service.Config.UpdateConfig(this, "CardNames", CardNames);
            }

            ImGui.PopID();
        }
    }

    private static unsafe void OnAddon0(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)args.Addon;
        if (addon == null) return;

        var textNode1 = addon->GetNodeById(18)->GetComponent()->UldManager.NodeList[19]->GetAsAtkTextNode();
        var textNode2 =
            addon->GetNodeById(38)->GetComponent()->UldManager.NodeList[8]->GetComponent()->UldManager.NodeList[1]->
                GetAsAtkTextNode();

        var origCardName1 = textNode1->NodeText.ExtractText();
        var origCardName2 = textNode2->NodeText.ExtractText();
        if (CardNames.TryGetValue(origCardName1, out var replacedName1))
            textNode1->SetText(replacedName1);
        else if (CardNames.TryGetValue(origCardName2, out var replacedName2)) textNode2->SetText(replacedName2);
    }

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddon0);

        base.Uninit();
    }
}
