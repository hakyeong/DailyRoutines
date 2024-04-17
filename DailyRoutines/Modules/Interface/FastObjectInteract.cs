using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using DailyRoutines.Windows;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;

namespace DailyRoutines.Modules;

[ModuleDescription("FastObjectInteractTitle", "FastObjectInteractDescription", ModuleCategories.Interface)]
public unsafe partial class FastObjectInteract : DailyModuleBase
{
    private class ObjectWaitSelected(GameObject* gameObject, string name, ObjectKind kind, float distance)
    {
        public GameObject* GameObject { get; set; } = gameObject;
        public string Name { get; set; } = name;
        public ObjectKind Kind { get; set; } = kind;
        public float Distance { get; set; } = distance;
    }

    private static bool ConfigAllowClickToTarget;
    private static bool ConfigWindowInvisibleWhenInteract;
    private static float ConfigFontScale = 1f;
    private static bool ConfigOnlyDisplayInViewRange;
    private static float ConfigMinButtonWidth = 300f;
    private static int ConfigMaxDisplayAmount = 5;
    private static HashSet<string> ConfigBlacklistKeys = new();
    private static HashSet<ObjectKind> ConfigSelectedKinds = new();
    private static bool ConfigLockWindow;

    private static string BlacklistKeyInput = string.Empty;
    private readonly List<ObjectWaitSelected> tempObjects = new(596);
    private readonly HashSet<float> distanceSet = new(596);
    private static float WindowWidth;

    private static readonly Dictionary<nint, ObjectWaitSelected> ObjectsWaitSelected = [];

    private static readonly Dictionary<ObjectKind, string> ObjectKindLoc = new()
    {
        { ObjectKind.BattleNpc, "战斗类 NPC (不建议)" },
        { ObjectKind.EventNpc, "一般类 NPC" },
        { ObjectKind.EventObj, "事件物体 (绝大多数要交互的都属于此类)" },
        { ObjectKind.Treasure, "宝箱" },
        { ObjectKind.Aetheryte, "以太之光" },
        { ObjectKind.GatheringPoint, "采集点" },
        { ObjectKind.MountType, "坐骑 (不建议)" },
        { ObjectKind.Companion, "宠物 (不建议)" },
        { ObjectKind.Retainer, "雇员" },
        { ObjectKind.Area, "地图传送相关" },
        { ObjectKind.Housing, "家具庭具" },
        { ObjectKind.CardStand, "固定类物体 (如无人岛采集点等)" },
        { ObjectKind.Ornament, "时尚配饰 (不建议)" }
    };

    private static Dictionary<uint, string>? ENpcTitles;

    public override void Init()
    {
        AddConfig(this, "MaxDisplayAmount", 5);
        AddConfig(this, "AllowClickToTarget", false);
        AddConfig(this, "WindowInvisibleWhenInteract", true);
        AddConfig(this, "FontScale", 1f);
        AddConfig(this, "SelectedKinds",
                                 new HashSet<ObjectKind>
                                 {
                                     ObjectKind.EventNpc, ObjectKind.EventObj, ObjectKind.Treasure,
                                     ObjectKind.Aetheryte, ObjectKind.GatheringPoint
                                 });
        AddConfig(this, "BlacklistKeys", new HashSet<string>());
        AddConfig(this, "MinButtonWidth", 300f);
        AddConfig(this, "OnlyDisplayInViewRange", false);
        AddConfig(this, "LockWindow", false);

        ConfigMaxDisplayAmount = GetConfig<int>(this, "MaxDisplayAmount");
        ConfigAllowClickToTarget = GetConfig<bool>(this, "AllowClickToTarget");
        ConfigWindowInvisibleWhenInteract = GetConfig<bool>(this, "WindowInvisibleWhenInteract");
        ConfigFontScale = GetConfig<float>(this, "FontScale");
        ConfigSelectedKinds = GetConfig<HashSet<ObjectKind>>(this, "SelectedKinds");
        ConfigBlacklistKeys = GetConfig<HashSet<string>>(this, "BlacklistKeys");
        ConfigMinButtonWidth = GetConfig<float>(this, "MinButtonWidth");
        ConfigOnlyDisplayInViewRange = GetConfig<bool>(this, "OnlyDisplayInViewRange");
        ConfigLockWindow = GetConfig<bool>(this, "LockWindow");

        ENpcTitles ??= LuminaCache.Get<ENpcResident>()
                              .Where(x => x.Unknown10)
                              .ToDictionary(x => x.RowId, x => x.Title.RawString);

        Service.Framework.Update += OnUpdate;

        Overlay ??= new Overlay(this, $"Daily Routines {Service.Lang.GetText("FastObjectInteractTitle")}");
        Overlay.Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.AlwaysAutoResize |
                        ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoCollapse;

        if (ConfigLockWindow)
            Overlay.Flags |= ImGuiWindowFlags.NoMove;
        else
            Overlay.Flags &= ~ImGuiWindowFlags.NoMove;
    }

    public override void ConfigUI()
    {
        ImGui.AlignTextToFramePadding();
        ImGui.Text($"{Service.Lang.GetText("FastObjectInteract-FontScale")}:");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(80f * ImGuiHelpers.GlobalScale);
        if (ImGui.InputFloat("###FontScaleInput", ref ConfigFontScale, 0f, 0f, ConfigFontScale.ToString(CultureInfo.InvariantCulture),
                             ImGuiInputTextFlags.EnterReturnsTrue))
        {
            ConfigFontScale = Math.Max(0.1f, ConfigFontScale);
            UpdateConfig(this, "FontScale", ConfigFontScale);
        }

        ImGui.AlignTextToFramePadding();
        ImGui.Text($"{Service.Lang.GetText("FastObjectInteract-MinButtonWidth")}:");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(80f * ImGuiHelpers.GlobalScale);
        if (ImGui.InputFloat("###MinButtonWidthInput", ref ConfigMinButtonWidth, 0, 0,
                             ConfigMinButtonWidth.ToString(CultureInfo.InvariantCulture),
                             ImGuiInputTextFlags.EnterReturnsTrue))
        {
            ConfigMinButtonWidth = Math.Max(1, ConfigMinButtonWidth);
            UpdateConfig(this, "MinButtonWidth", ConfigMinButtonWidth);
        }

        ImGui.AlignTextToFramePadding();
        ImGui.Text($"{Service.Lang.GetText("FastObjectInteract-MaxDisplayAmount")}:");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(80f * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("###MaxDisplayAmountInput", ref ConfigMaxDisplayAmount, 0, 0,
                           ImGuiInputTextFlags.EnterReturnsTrue))
        {
            ConfigMaxDisplayAmount = Math.Max(1, ConfigMaxDisplayAmount);
            UpdateConfig(this, "MaxDisplayAmount", ConfigMaxDisplayAmount);
        }

        ImGui.AlignTextToFramePadding();
        ImGui.Text($"{Service.Lang.GetText("FastObjectInteract-SelectedObjectKinds")}:");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(300f * ImGuiHelpers.GlobalScale);
        if (ImGui.BeginCombo("###ObjectKindsSelection",
                             Service.Lang.GetText("FastObjectInteract-SelectedObjectKindsAmount",
                                                  ConfigSelectedKinds.Count), ImGuiComboFlags.HeightLarge))
        {
            foreach (var kind in ObjectKindLoc)
            {
                var state = ConfigSelectedKinds.Contains(kind.Key);
                if (ImGui.Checkbox(kind.Value, ref state))
                {
                    if (!ConfigSelectedKinds.Remove(kind.Key))
                        ConfigSelectedKinds.Add(kind.Key);

                    UpdateConfig(this, "SelectedKinds", ConfigSelectedKinds);
                }
            }

            ImGui.EndCombo();
        }

        ImGui.AlignTextToFramePadding();
        ImGui.Text($"{Service.Lang.GetText("FastObjectInteract-BlacklistKeysList")}:");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(300f * ImGuiHelpers.GlobalScale);
        if (ImGui.BeginCombo("###BlacklistObjectsSelection",
                             Service.Lang.GetText("FastObjectInteract-BlacklistKeysListAmount",
                                                  ConfigBlacklistKeys.Count), ImGuiComboFlags.HeightLarge))
        {
            ImGui.SetNextItemWidth(250f * ImGuiHelpers.GlobalScale);
            ImGui.InputTextWithHint("###BlacklistKeyInput",
                                    $"{Service.Lang.GetText("FastObjectInteract-BlacklistKeysListInputHelp")}",
                                    ref BlacklistKeyInput, 100);
            ImGui.SameLine();
            if (ImGuiOm.ButtonIcon("###BlacklistKeyInputAdd", FontAwesomeIcon.Plus,
                                   Service.Lang.GetText("FastObjectInteract-Add")))
            {
                if (!ConfigBlacklistKeys.Add(BlacklistKeyInput)) return;

                UpdateConfig(this, "BlacklistKeys", ConfigBlacklistKeys);
            }

            ImGui.Separator();

            foreach (var key in ConfigBlacklistKeys)
            {
                if (ImGuiOm.ButtonIcon(key, FontAwesomeIcon.TrashAlt,
                                       Service.Lang.GetText("FastObjectInteract-Remove")))
                {
                    ConfigBlacklistKeys.Remove(key);
                    UpdateConfig(this, "BlacklistKeys", ConfigBlacklistKeys);
                }

                ImGui.SameLine();
                ImGui.Text(key);
            }

            ImGui.EndCombo();
        }

        if (ImGui.Checkbox(Service.Lang.GetText("FastObjectInteract-WindowInvisibleWhenInteract"),
                           ref ConfigWindowInvisibleWhenInteract))
            UpdateConfig(this, "WindowInvisibleWhenInteract", ConfigWindowInvisibleWhenInteract);

        if (ImGui.Checkbox(Service.Lang.GetText("FastObjectInteract-LockWindow"),
                           ref ConfigLockWindow))
        {
            UpdateConfig(this, "LockWindow", ConfigLockWindow);

            if (ConfigLockWindow)
                Overlay.Flags |= ImGuiWindowFlags.NoMove;
            else
                Overlay.Flags &= ~ImGuiWindowFlags.NoMove;
        }

        if (ImGui.Checkbox(Service.Lang.GetText("FastObjectInteract-OnlyDisplayInViewRange"),
                           ref ConfigOnlyDisplayInViewRange))
            UpdateConfig(this, "OnlyDisplayInViewRange", ConfigOnlyDisplayInViewRange);

        if (ImGui.Checkbox(Service.Lang.GetText("FastObjectInteract-AllowClickToTarget"),
                           ref ConfigAllowClickToTarget))
            UpdateConfig(this, "AllowClickToTarget", ConfigAllowClickToTarget);
    }

    public override void OverlayUI()
    {
        Service.Font.Axis14.Push();
        var colors = ImGui.GetStyle().Colors;
        ImGui.BeginGroup();
        foreach (var kvp in ObjectsWaitSelected)
        {
            if (kvp.Value.GameObject == null) continue;
            var interactState = CanInteract(kvp.Value.Kind, kvp.Value.Distance);

            if (ConfigAllowClickToTarget)
            {
                if (!interactState)
                {
                    ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.5f);
                    ImGui.PushStyleColor(ImGuiCol.ButtonActive, colors[(int)ImGuiCol.HeaderActive]);
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, colors[(int)ImGuiCol.HeaderHovered]);
                }

                ButtonText(kvp.Key.ToString(), kvp.Value.Name);

                if (!interactState)
                {
                    ImGui.PopStyleColor(2);
                    ImGui.PopStyleVar();
                }

                if (ImGui.BeginPopupContextItem($"{kvp.Value.Name}"))
                {
                    if (ImGui.MenuItem(Service.Lang.GetText("FastObjectInteract-AddToBlacklist")))
                    {
                        if (!ConfigBlacklistKeys.Add(AddToBlacklistNameRegex().Replace(kvp.Value.Name, "").Trim()))
                            return;
                        UpdateConfig(this, "BlacklistKeys", ConfigBlacklistKeys);
                    }

                    ImGui.EndPopup();
                }

                if (ImGui.IsItemClicked(ImGuiMouseButton.Left) && interactState)
                {
                    if (interactState) InteractWithObject(kvp.Value.GameObject, kvp.Value.Kind);
                }
                else if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                    TargetSystem.Instance()->Target = kvp.Value.GameObject;
            }
            else
            {
                ImGui.BeginDisabled(!interactState);
                if (ButtonText(kvp.Key.ToString(), kvp.Value.Name))
                    InteractWithObject(kvp.Value.GameObject, kvp.Value.Kind);

                if (ImGui.BeginPopupContextItem($"{kvp.Value.Name}"))
                {
                    if (ImGui.MenuItem(Service.Lang.GetText("FastObjectInteract-AddToBlacklist")))
                    {
                        if (!ConfigBlacklistKeys.Add(FastObjectInteractTitleRegex().Replace(kvp.Value.Name, "").Trim()))
                            return;
                        UpdateConfig(this, "BlacklistKeys", ConfigBlacklistKeys);
                    }

                    ImGui.EndPopup();
                }

                ImGui.EndDisabled();
            }
        }

        ImGui.EndGroup();
        WindowWidth = Math.Max(ConfigMinButtonWidth, ImGui.GetItemRectSize().X);
        Service.Font.Axis14.Pop();
    }

    private void OnUpdate(IFramework framework)
    {
        if (EzThrottler.Throttle("FastSelectObjects", 250))
        {
            if (Service.ClientState.LocalPlayer == null || Service.Condition[ConditionFlag.BetweenAreas])
            {
                ObjectsWaitSelected.Clear();
                WindowWidth = 0f;
                Overlay.IsOpen = false;
                return;
            }

            tempObjects.Clear();
            distanceSet.Clear();

            var localPlayer = (GameObject*)Service.ClientState.LocalPlayer.Address;
            var localPlayerY = localPlayer->Position.Y;

            foreach (var obj in Service.ObjectTable)
            {
                if (!obj.IsTargetable || obj.IsDead) continue;

                var objKind = obj.ObjectKind;
                if (!ConfigSelectedKinds.Contains(objKind)) continue;
                if (objKind == ObjectKind.EventNpc && !ENpcTitles.ContainsKey(obj.DataId)) continue;

                var objName = obj.Name.ExtractText();
                if (ConfigBlacklistKeys.Contains(objName)) continue;

                var gameObj = (GameObject*)obj.Address;
                if (ConfigOnlyDisplayInViewRange) if (!TargetSystem.Instance()->IsObjectInViewRange(gameObj)) continue;
                var objDistance = HelpersOm.GetGameDistanceFromObject(localPlayer, gameObj);
                var verticalDistance = localPlayerY - gameObj->Position.Y;
                if (objDistance > 10 || verticalDistance > 4) continue;

                var adjustedDistance = objDistance;
                while (distanceSet.Contains(adjustedDistance)) adjustedDistance += 0.001f;
                distanceSet.Add(adjustedDistance);

                if (objKind == ObjectKind.EventNpc &&
                    ENpcTitles.TryGetValue(obj.DataId, out var ENPCTitle) &&
                    !string.IsNullOrEmpty(ENPCTitle))
                {
                    var stringBuilder = new StringBuilder();
                    stringBuilder.Append('[');
                    stringBuilder.Append(ENPCTitle);
                    stringBuilder.Append(']');
                    stringBuilder.Append(' ');
                    stringBuilder.Append(obj.Name);
                    objName = stringBuilder.ToString();
                }

                if (tempObjects.Count > ConfigMaxDisplayAmount) break;
                tempObjects.Add(new ObjectWaitSelected(gameObj, objName, objKind, adjustedDistance));
            }

            tempObjects.Sort((a, b) => a.Distance.CompareTo(b.Distance));

            ObjectsWaitSelected.Clear();
            foreach (var tempObj in tempObjects) ObjectsWaitSelected.Add((nint)tempObj.GameObject, tempObj);

            if (!IsWindowShouldBeOpen())
            {
                Overlay.IsOpen = false;
                WindowWidth = 0f;
            }
            else
                Overlay.IsOpen = true;
        }
    }

    private static void InteractWithObject(GameObject* obj, ObjectKind kind)
    {
        TargetSystem.Instance()->Target = obj;
        TargetSystem.Instance()->InteractWithObject(obj);
        if (kind != ObjectKind.EventNpc)
            TargetSystem.Instance()->OpenObjectInteraction(obj);
    }

    private static bool IsWindowShouldBeOpen()
    {
        return ObjectsWaitSelected.Any() && (!ConfigWindowInvisibleWhenInteract || !IsOccupied());
    }

    private static bool CanInteract(ObjectKind kind, float distance)
    {
        return kind switch
        {
            ObjectKind.EventObj => distance <= 4,
            ObjectKind.EventNpc => distance <= 6.5,
            ObjectKind.Aetheryte => distance <= 9.5,
            _ => distance <= 2.6
        };
    }

    public static bool ButtonText(string id, string text)
    {
        ImGui.PushID(id);
        ImGui.SetWindowFontScale(ConfigFontScale);

        var textSize = ImGui.CalcTextSize(text);

        var cursorPos = ImGui.GetCursorScreenPos();
        var padding = ImGui.GetStyle().FramePadding;
        var buttonWidth = Math.Max(WindowWidth, textSize.X + (padding.X * 2));
        var result = ImGui.Button(string.Empty, new Vector2(buttonWidth, textSize.Y + (padding.Y * 2)));

        ImGui.GetWindowDrawList()
             .AddText(new Vector2(cursorPos.X + ((buttonWidth - textSize.X) / 2), cursorPos.Y + padding.Y),
                      ImGui.GetColorU32(ImGuiCol.Text), text);
        ImGui.SetWindowFontScale(1);
        ImGui.PopID();

        return result;
    }

    public override void Uninit()
    {
        Service.Framework.Update -= OnUpdate;
        ObjectsWaitSelected.Clear();

        base.Uninit();
    }

    [GeneratedRegex("\\[.*?\\]")]
    private static partial Regex AddToBlacklistNameRegex();

    [GeneratedRegex("\\[.*?\\]")]
    private static partial Regex FastObjectInteractTitleRegex();
}
