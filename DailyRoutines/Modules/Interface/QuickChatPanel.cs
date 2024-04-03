using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using DailyRoutines.Windows;
using Dalamud.Game.Addon.Events;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("QuickChatPanelTitle", "QuickChatPanelDescription", ModuleCategories.Interface)]
public unsafe class QuickChatPanel : DailyModuleBase
{
    public class SavedMacro : IEquatable<SavedMacro>
    {
        public uint Category { get; set; } // 0 - Individual; 1 - Shared
        public int Position { get; set; }
        public string Name { get; set; } = string.Empty;
        public uint IconID { get; set; } = 0;
        public List<string> CommandLines { get; set; } = [];
        public DateTime LastUpdateTime { get; set; } = DateTime.MinValue;

        public bool Equals(SavedMacro? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Category == other.Category && Position == other.Position;
        }

        public override bool Equals(object? obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((SavedMacro)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Category, Position);
        }
    }

    private static char[] SeIconChars = [];
    private static Vector2 ButtonPos = new(0);
    private static Vector2 WindowSize = new(200);
    private static IAddonEventHandle? MouseClickHandle;
    private static string MessageInput = string.Empty;

    private static List<string> ConfigSavedMessages = [];
    private static List<SavedMacro> ConfigSavedMacros = [];

    private static AtkUnitBase* AddonChatLog => (AtkUnitBase*)Service.Gui.GetAddonByName("ChatLog");

    public override void Init()
    {
        AddConfig(this, "SavedMessages", ConfigSavedMessages);
        ConfigSavedMessages = GetConfig<List<string>>(this, "SavedMessages");

        AddConfig(this, "SavedMacros", ConfigSavedMacros);
        ConfigSavedMacros = GetConfig<List<SavedMacro>>(this, "SavedMacros");

        var tempSeIconList = new List<char>();
        foreach (SeIconChar seIconChar in Enum.GetValues(typeof(SeIconChar)))
            tempSeIconList.Add((char)seIconChar);
        SeIconChars = [.. tempSeIconList];

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, "ChatLog", OnAddon);

        Overlay ??= new Overlay(this);
        Overlay.Flags |= ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize;
        Overlay.Flags &= ~ImGuiWindowFlags.AlwaysAutoResize;
        Overlay.Flags &= ~ImGuiWindowFlags.NoScrollbar;
    }

    public override void ConfigUI()
    {
        ImGui.BeginGroup();
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(ImGuiColors.DalamudOrange, $"{Service.Lang.GetText("QuickChatPanel-Messages")}:");

        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(ImGuiColors.DalamudOrange, $"{Service.Lang.GetText("QuickChatPanel-Macro")}:");
        ImGui.EndGroup();

        ImGui.SameLine();

        ImGui.BeginGroup();
        ImGui.SetNextItemWidth(240f * ImGuiHelpers.GlobalScale);
        if (ImGui.BeginCombo("###MessagesCombo",
                             Service.Lang.GetText("QuickChatPanel-SavedMessagesAmountText", ConfigSavedMessages.Count)))
        {
            ImGui.InputText("###MessageToSaveInput", ref MessageInput, 1000);
            ImGui.SameLine();
            if (ImGuiOm.ButtonIcon("###MessagesInputAdd", FontAwesomeIcon.Plus))
            {
                if (ConfigSavedMessages.Contains(MessageInput)) return;
                ConfigSavedMessages.Add(MessageInput);

                UpdateConfig(this, "SavedMessages", ConfigSavedMessages);
            }

            if (ConfigSavedMessages.Count > 0) ImGui.Separator();

            var messagesToDelete = new List<string>();
            foreach (var message in ConfigSavedMessages)
            {
                ImGuiOm.ButtonSelectable(message);

                if (ImGui.BeginPopupContextItem())
                {
                    if (ImGuiOm.ButtonSelectable(Service.Lang.GetText("Delete")))
                        messagesToDelete.Add(message);

                    ImGui.EndPopup();
                }
            }

            if (messagesToDelete.Count > 0)
            {
                messagesToDelete.ForEach(x => ConfigSavedMessages.Remove(x));
                UpdateConfig(this, "SavedMessages", ConfigSavedMessages);
            }

            ImGui.EndCombo();
        }

        ImGui.SetNextItemWidth(240f * ImGuiHelpers.GlobalScale);
        if (ImGui.BeginCombo("###MacroCombo",
                             Service.Lang.GetText("QuickChatPanel-SavedMacrosAmountText", ConfigSavedMacros.Count),
                             ImGuiComboFlags.HeightLargest))
        {
            var module = RaptureMacroModule.Instance();
            var leftChildSize = new Vector2(200 * ImGuiHelpers.GlobalScale, 300 * ImGuiHelpers.GlobalScale);
            if (ImGui.BeginChild("IndividualMacroComboSelect", leftChildSize))
            {
                ImGui.Text(Service.Lang.GetText("QuickChatPanel-IndividualMacros"));
                ImGui.Separator();

                var individualSpan = module->IndividualSpan;
                for (var i = 0; i < individualSpan.Length; i++)
                {
                    var macro = individualSpan.GetPointer(i);
                    if (macro == null) continue;

                    var name = macro->Name.ExtractText();
                    var icon = IconManager.GetIcon(macro->IconId);
                    if (string.IsNullOrEmpty(name) || icon == null) continue;

                    var currentSavedMacro = (*macro).ToSavedMacro();
                    currentSavedMacro.Position = i;
                    currentSavedMacro.Category = 0;

                    ImGui.PushID($"{currentSavedMacro.Category}-{currentSavedMacro.Position}");
                    if (ImGuiOm.SelectableImageWithText(icon.ImGuiHandle, new(24), name,
                                                        ConfigSavedMacros.Contains(currentSavedMacro),
                                                        ImGuiSelectableFlags.DontClosePopups))
                    {
                        if (!ConfigSavedMacros.Remove(currentSavedMacro))
                        {
                            ConfigSavedMacros.Add(currentSavedMacro);
                            UpdateConfig(this, "SavedMacros", ConfigSavedMacros);
                        }
                    }

                    if (ConfigSavedMacros.Contains(currentSavedMacro) && ImGui.BeginPopupContextItem())
                    {
                        ImGui.TextColored(ImGuiColors.DalamudOrange,
                                          $"{Service.Lang.GetText("QuickChatPanel-LastUpdateTime")}:");

                        ImGui.SameLine();
                        ImGui.Text(
                            $"{ConfigSavedMacros.FirstOrDefault(x => x.Category == 0 && x.Position == i)?.LastUpdateTime}");

                        ImGui.Separator();

                        if (ImGuiOm.SelectableTextCentered(Service.Lang.GetText("Refresh")))
                        {
                            if (ConfigSavedMacros.Remove(currentSavedMacro))
                            {
                                ConfigSavedMacros.Add(currentSavedMacro);
                                UpdateConfig(this, "SavedMacros", ConfigSavedMacros);
                            }
                        }

                        ImGui.EndPopup();
                    }

                    ImGui.PopID();
                }

                ImGui.EndChild();
            }

            ImGui.SameLine();
            if (ImGui.BeginChild("SharedMacroComboSelect", leftChildSize))
            {
                ImGui.Text(Service.Lang.GetText("QuickChatPanel-SharedMacros"));
                ImGui.Separator();

                var individualSpan = module->SharedSpan;
                for (var i = 0; i < individualSpan.Length; i++)
                {
                    var macro = individualSpan.GetPointer(i);
                    if (macro == null) continue;

                    var name = macro->Name.ExtractText();
                    var icon = IconManager.GetIcon(macro->IconId);
                    if (string.IsNullOrEmpty(name) || icon == null) continue;

                    var currentSavedMacro = (*macro).ToSavedMacro();
                    currentSavedMacro.Position = i;
                    currentSavedMacro.Category = 1;

                    if (ImGuiOm.SelectableImageWithText(icon.ImGuiHandle, new(24), name,
                                                        ConfigSavedMacros.Contains(currentSavedMacro),
                                                        ImGuiSelectableFlags.DontClosePopups))
                    {
                        if (!ConfigSavedMacros.Remove(currentSavedMacro))
                        {
                            ConfigSavedMacros.Add(currentSavedMacro);
                            UpdateConfig(this, "SavedMacros", ConfigSavedMacros);
                        }
                    }
                }

                ImGui.EndChild();
            }

            ImGui.EndCombo();
        }

        ImGui.EndGroup();
    }

    public override void OverlayUI()
    {
        var textInputNode = AddonChatLog->GetNodeById(5);
        if (textInputNode == null) return;

        var buttonPos = new Vector2(textInputNode->X + textInputNode->Width, textInputNode->ScreenY - 3);

        ImGui.SetWindowSize(new(WindowSize.X + (20 * ImGuiHelpers.GlobalScale), 240 * ImGuiHelpers.GlobalScale));
        ImGui.SetWindowPos(buttonPos with { Y = buttonPos.Y - ImGui.GetWindowSize().Y - 5 });

        if (ImGui.BeginTabBar("###QuickChatPanel"))
        {
            if (ImGui.BeginTabItem(Service.Lang.GetText("QuickChatPanel-Messages")))
            {
                Service.Font.Axis14.Push();
                ImGui.SetWindowFontScale(1.5f);
                var maxTextWidth = 200f;
                for (var i = 0; i < ConfigSavedMessages.Count; i++)
                {
                    var message = ConfigSavedMessages[i];

                    var textWidth = ImGui.CalcTextSize(message).X;
                    maxTextWidth = Math.Max(textWidth + 64, maxTextWidth);

                    ImGuiOm.SelectableTextCentered(message);

                    if (ImGui.IsItemClicked(ImGuiMouseButton.Left)) ImGui.SetClipboardText(message);

                    if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) Chat.Instance.SendMessage(message);

                    ImGuiOm.TooltipHover(Service.Lang.GetText("QuickChatPanel-SendMessageHelp"));

                    if (i != ConfigSavedMessages.Count - 1)
                        ImGui.Separator();
                }

                Service.Font.Axis14.Pop();
                ImGui.SetWindowFontScale(1f);

                ImGui.SetWindowSize(new(Math.Max(WindowSize.X, maxTextWidth), 240 * ImGuiHelpers.GlobalScale));

                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem(Service.Lang.GetText("QuickChatPanel-Macro")))
            {
                Service.Font.Axis14.Push();
                ImGui.SetWindowFontScale(1.5f);
                var maxTextWidth = 200f;
                for (var i = 0; i < ConfigSavedMacros.Count; i++)
                {
                    var macro = ConfigSavedMacros[i];

                    var name = macro.Name;
                    var icon = IconManager.GetIcon(macro.IconID);
                    if (string.IsNullOrEmpty(name) || icon == null) continue;

                    var textWidth = ImGui.CalcTextSize(name).X;
                    maxTextWidth = Math.Max(textWidth + 64, maxTextWidth);

                    if (ImGuiOm.SelectableImageWithText(icon.ImGuiHandle, new(24), name, false))
                        MacroManager.Execute(macro.CommandLines);

                    if (i != ConfigSavedMacros.Count - 1)
                        ImGui.Separator();
                }

                ImGui.SetWindowFontScale(1f);
                Service.Font.Axis14.Pop();

                ImGui.SetWindowSize(new(Math.Max(WindowSize.X, maxTextWidth), 240 * ImGuiHelpers.GlobalScale));

                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem(Service.Lang.GetText("QuickChatPanel-SpecialIconChar")))
            {
                Service.Font.Axis14.Push();
                ImGui.BeginGroup();
                ImGui.SetWindowFontScale(1.5f);
                for (var i = 0; i < SeIconChars.Length; i++)
                {
                    var icon = SeIconChars[i];
                    if (ImGui.Button($"{icon}", new(64))) ImGui.SetClipboardText(icon.ToString());

                    ImGuiOm.TooltipHover($"0x{(int)icon:X4}");

                    if ((i + 1) % 7 != 0) ImGui.SameLine();
                }

                ImGui.EndGroup();

                WindowSize = ImGui.GetItemRectSize();
                ImGui.SetWindowFontScale(1f);
                Service.Font.Axis14.Pop();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        ImGui.SameLine();
        if (ImGuiOm.ButtonIcon("OpenQuickChatPanelSettings", FontAwesomeIcon.Cog))
        {
            P.Main.IsOpen ^= true;
            if (P.Main.IsOpen)
            {
                Main.SearchString = Service.Lang.GetText("QuickChatPanelTitle");
                return;
            }

            Main.SearchString = string.Empty;
        }
    }

    private void OnAddon(AddonEvent type, AddonArgs args)
    {
        if (Service.ClientState.LocalPlayer == null) return;
        if (AddonChatLog == null) return;

        var textInputNode = AddonChatLog->GetNodeById(5);
        if (textInputNode == null) return;

        ButtonPos = new Vector2(textInputNode->X + textInputNode->Width, textInputNode->Y - 3);

        AddonChatLog->RootNode->SetWidth(
            (ushort)Math.Max(AddonChatLog->X, textInputNode->X + textInputNode->Width + 48));

        AtkResNode* iconNode = null;
        for (var i = 0; i < AddonChatLog->UldManager.NodeListCount; i++)
        {
            var node = AddonChatLog->UldManager.NodeList[i];
            if (node->NodeID == 10001)
            {
                iconNode = node;
                break;
            }
        }

        if (iconNode is null)
            MakeIconNode(10001, ButtonPos, 46);
        else
            iconNode->SetPositionFloat(ButtonPos.X, ButtonPos.Y);
    }

    private void MakeIconNode(uint nodeId, Vector2 position, int icon)
    {
        var imageNode = AddonManager.MakeImageNode(nodeId, new AddonManager.PartInfo(0, 0, 64, 64));
        imageNode->AtkResNode.NodeFlags = NodeFlags.AnchorTop | NodeFlags.AnchorLeft | NodeFlags.Visible |
                                          NodeFlags.Enabled | NodeFlags.EmitsEvents;
        imageNode->WrapMode = 1;
        imageNode->Flags = (byte)ImageNodeFlags.AutoFit;

        imageNode->LoadIconTexture(icon, 0);
        imageNode->AtkResNode.ToggleVisibility(true);

        imageNode->AtkResNode.SetWidth(48);
        imageNode->AtkResNode.SetHeight(48);
        imageNode->AtkResNode.SetPositionShort((short)position.X, (short)position.Y);

        AddonManager.LinkNodeAtEnd((AtkResNode*)imageNode, AddonChatLog);

        imageNode->AtkResNode.NodeFlags |= NodeFlags.RespondToMouse | NodeFlags.EmitsEvents | NodeFlags.HasCollision;
        AddonChatLog->UpdateCollisionNodeList(false);
        MouseClickHandle ??=
            Service.AddonEvent.AddEvent((nint)AddonChatLog, (nint)(&imageNode->AtkResNode), AddonEventType.MouseClick,
                                        OnEvent);
    }

    private void OnEvent(AddonEventType atkEventType, IntPtr atkUnitBase, IntPtr atkResNode)
    {
        Overlay.IsOpen ^= true;
    }

    private static void FreeNode()
    {
        if (AddonChatLog == null) return;

        for (var i = 0; i < AddonChatLog->UldManager.NodeListCount; i++)
        {
            var node = AddonChatLog->UldManager.NodeList[i];
            if (node->NodeID == 10001)
            {
                AddonManager.UnlinkAndFreeImageNode((AtkImageNode*)node, AddonChatLog);
                Service.AddonEvent.RemoveEvent(MouseClickHandle);
            }
        }
    }

    public override void Uninit()
    {
        FreeNode();
        Service.AddonLifecycle.UnregisterListener(OnAddon);

        base.Uninit();
    }
}
