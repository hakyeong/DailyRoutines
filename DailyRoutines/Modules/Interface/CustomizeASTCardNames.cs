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
        AddConfig(this, "CardNames", CardNames);
        CardNames = GetConfig<Dictionary<string, string>>(this, "CardNames");

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
                UpdateConfig(this, "CardNames", CardNames);
            }

            ImGui.PopID();
        }
    }

    private static unsafe void OnAddon0(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)args.Addon;
        if (addon == null) return;

        // 完整 HUD
        var completeComponent = addon->GetNodeById(18);
        if (completeComponent != null)
        {
            var node = completeComponent->GetComponent()->GetTextNodeById(2);
            if (node != null)
            {
                var completeTextNode = node->GetAsAtkTextNode();
                var origCardName = completeTextNode->NodeText.ExtractText();
                if (CardNames.TryGetValue(origCardName, out var replacedName))
                    completeTextNode->SetText(replacedName);
            }
        }

        // 轻量 HUD
        var liteComponent = addon->GetNodeById(38);
        if (liteComponent != null)
        {
            var node = liteComponent->GetComponent()->UldManager.NodeList[8];
            if (node != null)
            {
                var node1 = node->GetComponent()->GetTextNodeById(2);
                if (node1 != null)
                {
                    var liteNode = node1->GetAsAtkTextNode();
                    var origCardName = liteNode->NodeText.ExtractText();
                    if (CardNames.TryGetValue(origCardName, out var replacedName))
                        liteNode->SetText(replacedName);
                }
            }
        }
    }

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddon0);

        base.Uninit();
    }
}
