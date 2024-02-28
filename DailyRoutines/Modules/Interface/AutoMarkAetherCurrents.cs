using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Interface.Colors;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoMarkAetherCurrentsTitle", "AutoMarkAetherCurrentsDescription", ModuleCategories.Interface)]
public class AutoMarkAetherCurrents : IDailyModule
{
    public bool Initialized { get; set; }
    public bool WithConfigUI => true;

    private class AetherCurrent(uint index, Vector3 pos)
    {
        public uint Index { get; } = index;
        public Vector3 Position { get; } = pos;
    }

    #region PresetData

    private readonly HashSet<uint> ValidTerritories =
    [
        397, 398, 399, 400, 401, 612, 613, 614, 620, 621, 622, 813, 814, 815, 816, 817, 818, 956, 957, 958, 959, 960,
        961
    ];

    // 坐标可以全部用 Lumina 获取，但是 SE 调了部分风脉在 UI 上的对应顺序，所以就干脆所有都硬编码了
    private readonly List<(uint TerritoryID, AetherCurrent AetherCurrent)> PresetAetherCurrentsData =
    [
        // 库尔札斯西部高地
        (397, new AetherCurrent(0, new Vector3(402.03f, 191.54f, 561.42f))),
        (397, new AetherCurrent(1, new Vector3(424.96f, 164.31f, -536.91f))),
        (397, new AetherCurrent(2, new Vector3(-332.89f, 126.84f, -29.95f))),
        (397, new AetherCurrent(3, new Vector3(-660.14f, 135.55f, -376.63f))),
        // 龙堡参天高地
        (398, new AetherCurrent(0, new Vector3(765.01f, -15.85f, 289.08f))),
        (398, new AetherCurrent(1, new Vector3(433.55f, -47.77f, -286.24f))),
        (398, new AetherCurrent(2, new Vector3(-480.28f, -5.97f, -425.28f))),
        (398, new AetherCurrent(3, new Vector3(406.76f, -89.79f, 686.61f))),
        // 龙堡内陆低地
        (399, new AetherCurrent(0, new Vector3(729.23f, 134.97f, 150.91f))),
        (399, new AetherCurrent(1, new Vector3(98.91f, 73.09f, -174.36f))),
        (399, new AetherCurrent(2, new Vector3(-487.47f, 144.67f, -285.33f))),
        (399, new AetherCurrent(3, new Vector3(-452.38f, 138.12f, 678.22f))),
        // 翻云雾海
        (400, new AetherCurrent(0, new Vector3(421.16f, -43.06f, 661.77f))),
        (400, new AetherCurrent(1, new Vector3(-93.79f, -6.75f, 223.84f))),
        (400, new AetherCurrent(2, new Vector3(-775.27f, 123.76f, 243.70f))),
        (400, new AetherCurrent(3, new Vector3(340.02f, -25.37f, -130.54f))),
        // 阿巴拉提亚云海
        (401, new AetherCurrent(0, new Vector3(-747.10f, -57.10f, 163.84f))),
        (401, new AetherCurrent(1, new Vector3(-759.43f, -9.20f, -110.86f))),
        (401, new AetherCurrent(2, new Vector3(-180.32f, -14.92f, -543.11f))),
        (401, new AetherCurrent(3, new Vector3(-564.81f, -36.68f, -349.08f))),
        // 基拉巴尼亚边区
        (612, new AetherCurrent(0, new Vector3(-487.27f, 76.73f, -249.56f))),
        (612, new AetherCurrent(1, new Vector3(155.84f, 53.35f, -499.43f))),
        (612, new AetherCurrent(2, new Vector3(322.59f, 88.98f, 10.51f))),
        (612, new AetherCurrent(3, new Vector3(743.03f, 181.01f, -214.05f))),
        // 基拉巴尼亚山区
        (620, new AetherCurrent(0, new Vector3(202.87f, 133.93f, -753.12f))),
        (620, new AetherCurrent(1, new Vector3(-271.23f, 157.94f, -280.23f))),
        (620, new AetherCurrent(2, new Vector3(-485.21f, 304.47f, 247.41f))),
        (620, new AetherCurrent(3, new Vector3(146.63f, 303.76f, 460.82f))),
        // 基拉巴尼亚湖区
        (621, new AetherCurrent(0, new Vector3(-380.19f, 10.06f, 16.92f))),
        (621, new AetherCurrent(1, new Vector3(109.61f, 42.01f, 788.63f))),
        (621, new AetherCurrent(2, new Vector3(261.53f, 78.40f, 69.96f))),
        (621, new AetherCurrent(3, new Vector3(683.41f, 70.00f, 521.17f))),
        // 红玉海
        (613, new AetherCurrent(0, new Vector3(423.56f, 15.83f, 801.57f))),
        (613, new AetherCurrent(1, new Vector3(21.29f, 24.01f, -623.62f))),
        (613, new AetherCurrent(2, new Vector3(694.77f, 1.91f, -53.46f))),
        (613, new AetherCurrent(3, new Vector3(-805.78f, 36.58f, 235.21f))),
        // 延夏
        (614, new AetherCurrent(0, new Vector3(497.31f, 16.36f, 402.49f))),
        (614, new AetherCurrent(1, new Vector3(163.58f, 144.63f, -11.91f))),
        (614, new AetherCurrent(2, new Vector3(457.51f, 32.38f, 822.41f))),
        (614, new AetherCurrent(3, new Vector3(-97.42f, 13.28f, 563.72f))),
        // 太阳神草原
        (622, new AetherCurrent(0, new Vector3(570.28f, -19.51f, 438.17f))),
        (622, new AetherCurrent(1, new Vector3(232.01f, 93.39f, -515.79f))),
        (622, new AetherCurrent(2, new Vector3(105.61f, 116.04f, -49.70f))),
        (622, new AetherCurrent(3, new Vector3(-693.83f, 7.26f, 658.90f))),
        // 雷克兰德
        (813, new AetherCurrent(0, new Vector3(554.28f, 17.95f, 352.10f))),
        (813, new AetherCurrent(1, new Vector3(613.24f, 24.02f, -231.13f))),
        (813, new AetherCurrent(2, new Vector3(-149.80f, 15.28f, -102.50f))),
        (813, new AetherCurrent(3, new Vector3(-619.64f, 51.50f, -199.10f))),
        // 珂露西亚岛
        (814, new AetherCurrent(0, new Vector3(650.57f, 0.35f, 556.39f))),
        (814, new AetherCurrent(1, new Vector3(-651.17f, 0f, 588.41f))),
        (814, new AetherCurrent(2, new Vector3(623.75f, 285.94f, -555.25f))),
        (814, new AetherCurrent(3, new Vector3(-62.90f, 345.12f, -16.53f))),
        // 安穆·艾兰
        (815, new AetherCurrent(0, new Vector3(446.08f, -60.55f, -523.69f))),
        (815, new AetherCurrent(1, new Vector3(344.68f, -66.53f, 538.94f))),
        (815, new AetherCurrent(2, new Vector3(-343.80f, 46.98f, -235.43f))),
        (815, new AetherCurrent(3, new Vector3(158.80f, -61.09f, 674.89f))),
        // 伊尔美格
        (816, new AetherCurrent(0, new Vector3(-231.41f, 4.70f, 160.84f))),
        (816, new AetherCurrent(1, new Vector3(12.85f, 110.75f, -851.25f))),
        (816, new AetherCurrent(2, new Vector3(432.48f, 90.44f, -770.40f))),
        (816, new AetherCurrent(3, new Vector3(-9.00f, 89.31f, -247.64f))),
        // 拉凯提卡大森林
        (817, new AetherCurrent(0, new Vector3(-405.95f, 7.17f, 506.54f))),
        (817, new AetherCurrent(1, new Vector3(-141.57f, -0.88f, 49.76f))),
        (817, new AetherCurrent(2, new Vector3(338.64f, 24.15f, 203.18f))),
        (817, new AetherCurrent(3, new Vector3(681.14f, -39.20f, -262.75f))),
        // 黑风海
        (818, new AetherCurrent(0, new Vector3(358.21f, 396.55f, -715.90f))),
        (818, new AetherCurrent(1, new Vector3(50.21f, 380.10f, -512.07f))),
        (818, new AetherCurrent(2, new Vector3(339.12f, 298.72f, -280.02f))),
        (818, new AetherCurrent(3, new Vector3(-774.23f, 63.19f, -97.71f))),
        // 迷津
        (956, new AetherCurrent(0, new Vector3(346.53f, 209.35f, -767.74f))),
        (956, new AetherCurrent(1, new Vector3(748.56f, 106.71f, 66.76f))),
        (956, new AetherCurrent(2, new Vector3(-547.73f, -18.02f, 661.87f))),
        (956, new AetherCurrent(3, new Vector3(-128.07f, -20.52f, 676.72f))),
        (956, new AetherCurrent(4, new Vector3(-316.28f, 79.76f, -395.31f))),
        (956, new AetherCurrent(5, new Vector3(32.33f, 72.83f, -286.27f))),
        (956, new AetherCurrent(6, new Vector3(497.11f, 73.42f, -267.23f))),
        (956, new AetherCurrent(7, new Vector3(-176.38f, -10.10f, -242.24f))),
        (956, new AetherCurrent(8, new Vector3(-505.14f, -21.82f, -122.60f))),
        (956, new AetherCurrent(9, new Vector3(46.28f, -29.80f, 178.88f))),
        // 萨维奈岛
        (957, new AetherCurrent(0, new Vector3(-176.10f, 21.53f, 537.84f))),
        (957, new AetherCurrent(1, new Vector3(-49.27f, 94.07f, -710.76f))),
        (957, new AetherCurrent(2, new Vector3(118.47f, 4.93f, -343.87f))),
        (957, new AetherCurrent(3, new Vector3(550.02f, 25.48f, -159.08f))),
        (957, new AetherCurrent(4, new Vector3(303.92f, 0.28f, 473.66f))),
        (957, new AetherCurrent(5, new Vector3(-479.44f, 72.90f, -561.81f))),
        (957, new AetherCurrent(6, new Vector3(-114.46f, 87.09f, -288.29f))),
        (957, new AetherCurrent(7, new Vector3(93.12f, 36.68f, -447.86f))),
        (957, new AetherCurrent(8, new Vector3(294.40f, 4.10f, 425.12f))),
        (957, new AetherCurrent(9, new Vector3(53.19f, 11.39f, 187.41f))),
        // 加雷马
        (958, new AetherCurrent(0, new Vector3(-184.22f, 31.94f, 423.61f))),
        (958, new AetherCurrent(1, new Vector3(194.82f, -12.84f, 644.31f))),
        (958, new AetherCurrent(2, new Vector3(83.09f, 1.53f, 102.02f))),
        (958, new AetherCurrent(3, new Vector3(405.30f, -2.24f, 520.32f))),
        (958, new AetherCurrent(4, new Vector3(-516.09f, 42.47f, 67.84f))),
        (958, new AetherCurrent(5, new Vector3(382.18f, 25.90f, -482.20f))),
        (958, new AetherCurrent(6, new Vector3(-602.03f, 34.32f, -325.85f))),
        (958, new AetherCurrent(7, new Vector3(79.91f, 37.89f, -518.18f))),
        (958, new AetherCurrent(8, new Vector3(134.93f, 14.40f, -172.25f))),
        (958, new AetherCurrent(9, new Vector3(-144.92f, 17.58f, -420.52f))),
        // 叹息海
        (959, new AetherCurrent(0, new Vector3(42.59f, 124.01f, -167.03f))),
        (959, new AetherCurrent(1, new Vector3(-482.74f, -154.95f, -595.71f))),
        (959, new AetherCurrent(2, new Vector3(316.40f, -154.98f, -595.52f))),
        (959, new AetherCurrent(3, new Vector3(29.10f, -47.74f, -550.41f))),
        (959, new AetherCurrent(4, new Vector3(-128.00f, 66.35f, -68.24f))),
        (959, new AetherCurrent(5, new Vector3(591.38f, 149.36f, 114.94f))),
        (959, new AetherCurrent(6, new Vector3(388.36f, 99.92f, 306.07f))),
        (959, new AetherCurrent(7, new Vector3(652.98f, -160.69f, -405.08f))),
        (959, new AetherCurrent(8, new Vector3(-733.62f, -139.66f, -733.28f))),
        (959, new AetherCurrent(9, new Vector3(21.71f, -133.50f, -385.73f))),
        // 厄尔庇斯
        (961, new AetherCurrent(0, new Vector3(628.24f, 8.32f, 107.90f))),
        (961, new AetherCurrent(1, new Vector3(-754.75f, -36.01f, 411.13f))),
        (961, new AetherCurrent(2, new Vector3(151.67f, 7.67f, 2.55f))),
        (961, new AetherCurrent(3, new Vector3(-144.54f, -26.23f, 551.52f))),
        (961, new AetherCurrent(4, new Vector3(-481.41f, -28.58f, 490.56f))),
        (961, new AetherCurrent(5, new Vector3(-402.92f, 327.76f, -691.32f))),
        (961, new AetherCurrent(6, new Vector3(-555.62f, 158.12f, 172.43f))),
        (961, new AetherCurrent(7, new Vector3(-392.05f, 173.72f, -293.60f))),
        (961, new AetherCurrent(8, new Vector3(-761.71f, 160.01f, -108.99f))),
        (961, new AetherCurrent(9, new Vector3(-255.51f, 143.08f, -36.97f))),
        // 天外天垓
        (960, new AetherCurrent(0, new Vector3(-333.54f, 270.85f, -361.49f))),
        (960, new AetherCurrent(1, new Vector3(13.12f, 275.57f, -756.40f))),
        (960, new AetherCurrent(2, new Vector3(661.78f, 439.98f, 411.76f))),
        (960, new AetherCurrent(3, new Vector3(539.27f, 438.00f, 239.40f))),
        (960, new AetherCurrent(4, new Vector3(424.57f, 283.38f, -679.76f))),
        (960, new AetherCurrent(5, new Vector3(-238.79f, 320.39f, -295.14f))),
        (960, new AetherCurrent(6, new Vector3(-385.22f, 262.52f, -629.85f))),
        (960, new AetherCurrent(7, new Vector3(751.88f, 439.98f, 357.90f))),
        (960, new AetherCurrent(8, new Vector3(637.20f, 439.24f, 289.67f))),
        (960, new AetherCurrent(9, new Vector3(567.50f, 440.93f, 402.14f)))
    ];

    #endregion

    private static Dictionary<uint, HashSet<AetherCurrent>> SelectedAetherCurrents = new()
    {
        { 397, [] },
        { 398, [] },
        { 399, [] },
        { 400, [] },
        { 401, [] },
        { 612, [] },
        { 613, [] },
        { 614, [] },
        { 620, [] },
        { 621, [] },
        { 622, [] },
        { 813, [] },
        { 814, [] },
        { 815, [] },
        { 816, [] },
        { 817, [] },
        { 818, [] },
        { 956, [] },
        { 957, [] },
        { 958, [] },
        { 959, [] },
        { 960, [] },
        { 961, [] }
    };

    public void Init()
    {
        Service.Hook.InitializeFromAttributes(this);
        Service.ClientState.TerritoryChanged += OnZoneChanged;
    }

    public void ConfigUI()
    {
        if (ImGui.Button(Service.Lang.GetText("AutoMarkAetherCurrents-RefreshDisplay")))
            MarkAetherCurrents(Service.ClientState.TerritoryType, false);

        ImGui.SameLine();
        if (ImGui.Button(Service.Lang.GetText("AutoMarkAetherCurrents-DisplayLeftCurrents")))
            MarkAetherCurrents(Service.ClientState.TerritoryType, true);

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Service.Lang.GetText("AutoMarkAetherCurrents-DisplayLeftCurrentsHelp"));

        ImGui.SameLine();
        ImGuiOm.HelpMarker(Service.Lang.GetText("AutoMarkAetherCurrents-FieldMarkerHelp"));

        ImGui.TextColored(ImGuiColors.DalamudOrange,
                          $"{Service.Lang.GetText("AutoMarkAetherCurrents-ManuallySelectCurrent")}:");
        ImGuiOm.HelpMarker(Service.Lang.GetText("AutoMarkAetherCurrents-ManuallySelectCurrentHelp"), 25f);

        if (ImGui.BeginTabBar("AutoMarkAetherCurrent-ManuallySelect"))
        {
            if (ImGui.BeginTabItem("3.0"))
            {
                // 左半边
                ImGui.BeginGroup();
                ImGui.BeginGroup();
                ImGui.AlignTextToFramePadding();
                ImGui.Text("库尔札斯西部高地");
                ImGui.AlignTextToFramePadding();
                ImGui.Text("龙堡内陆低地");
                ImGui.AlignTextToFramePadding();
                ImGui.Text("阿巴拉提亚云海");
                ImGui.EndGroup();

                ImGui.SameLine();
                ImGui.BeginGroup();
                DrawManuallySelectGroupOld("库尔札斯西部高地", 397, ref SelectedAetherCurrents);
                DrawManuallySelectGroupOld("龙堡内陆低地", 399, ref SelectedAetherCurrents);
                DrawManuallySelectGroupOld("阿巴拉提亚云海", 401, ref SelectedAetherCurrents);
                ImGui.EndGroup();
                ImGui.EndGroup();

                // 右半边
                ImGui.SameLine();
                ImGui.BeginGroup();
                ImGui.BeginGroup();
                ImGui.AlignTextToFramePadding();
                ImGui.Text("龙堡参天高地");
                ImGui.AlignTextToFramePadding();
                ImGui.Text("翻云雾海");
                ImGui.EndGroup();

                ImGui.SameLine();
                ImGui.BeginGroup();
                DrawManuallySelectGroupOld("龙堡参天高地", 398, ref SelectedAetherCurrents);
                DrawManuallySelectGroupOld("翻云雾海", 400, ref SelectedAetherCurrents);
                ImGui.EndGroup();
                ImGui.EndGroup();

                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("4.0"))
            {
                // 左半边
                ImGui.BeginGroup();
                ImGui.BeginGroup();
                ImGui.AlignTextToFramePadding();
                ImGui.Text("基拉巴尼亚边区");
                ImGui.AlignTextToFramePadding();
                ImGui.Text("基拉巴尼亚山区");
                ImGui.AlignTextToFramePadding();
                ImGui.Text("基拉巴尼亚湖区");
                ImGui.EndGroup();

                ImGui.SameLine();
                ImGui.BeginGroup();
                DrawManuallySelectGroupOld("基拉巴尼亚边区", 612, ref SelectedAetherCurrents);
                DrawManuallySelectGroupOld("基拉巴尼亚山区", 620, ref SelectedAetherCurrents);
                DrawManuallySelectGroupOld("基拉巴尼亚湖区", 621, ref SelectedAetherCurrents);
                ImGui.EndGroup();
                ImGui.EndGroup();

                // 右半边
                ImGui.SameLine();
                ImGui.BeginGroup();
                ImGui.BeginGroup();
                ImGui.AlignTextToFramePadding();
                ImGui.Text("红玉海");
                ImGui.AlignTextToFramePadding();
                ImGui.Text("延夏");
                ImGui.AlignTextToFramePadding();
                ImGui.Text("太阳神草原");
                ImGui.EndGroup();

                ImGui.SameLine();
                ImGui.BeginGroup();
                DrawManuallySelectGroupOld("红玉海", 613, ref SelectedAetherCurrents);
                DrawManuallySelectGroupOld("延夏", 614, ref SelectedAetherCurrents);
                DrawManuallySelectGroupOld("太阳神草原", 622, ref SelectedAetherCurrents);
                ImGui.EndGroup();
                ImGui.EndGroup();

                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("5.0"))
            {
                // 左半边
                ImGui.BeginGroup();
                ImGui.BeginGroup();
                ImGui.AlignTextToFramePadding();
                ImGui.Text("雷克兰德");
                ImGui.AlignTextToFramePadding();
                ImGui.Text("伊尔美格");
                ImGui.AlignTextToFramePadding();
                ImGui.Text("拉凯提卡大森林");
                ImGui.EndGroup();

                ImGui.SameLine();
                ImGui.BeginGroup();
                DrawManuallySelectGroupOld("雷克兰德", 813, ref SelectedAetherCurrents);
                DrawManuallySelectGroupOld("伊尔美格", 816, ref SelectedAetherCurrents);
                DrawManuallySelectGroupOld("拉凯提卡大森林", 817, ref SelectedAetherCurrents);
                ImGui.EndGroup();
                ImGui.EndGroup();

                // 右半边
                ImGui.SameLine();
                ImGui.BeginGroup();
                ImGui.BeginGroup();
                ImGui.AlignTextToFramePadding();
                ImGui.Text("安穆·艾兰");
                ImGui.AlignTextToFramePadding();
                ImGui.Text("珂露西亚岛");
                ImGui.AlignTextToFramePadding();
                ImGui.Text("黑风海");
                ImGui.EndGroup();

                ImGui.SameLine();
                ImGui.BeginGroup();
                DrawManuallySelectGroupOld("安穆·艾兰", 815, ref SelectedAetherCurrents);
                DrawManuallySelectGroupOld("珂露西亚岛", 814, ref SelectedAetherCurrents);
                DrawManuallySelectGroupOld("黑风海", 818, ref SelectedAetherCurrents);
                ImGui.EndGroup();
                ImGui.EndGroup();

                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("6.0"))
            {
                // 左半边
                ImGui.BeginGroup();
                ImGui.BeginGroup();
                ImGui.AlignTextToFramePadding();
                ImGui.Text("迷津");
                ImGui.AlignTextToFramePadding();
                ImGui.Text("加雷马");
                ImGui.AlignTextToFramePadding();
                ImGui.Text("厄尔庇斯");
                ImGui.EndGroup();

                ImGui.SameLine();
                ImGui.BeginGroup();
                DrawManuallySelectGroupNew("迷津", 956, ref SelectedAetherCurrents);
                DrawManuallySelectGroupNew("加雷马", 958, ref SelectedAetherCurrents);
                DrawManuallySelectGroupNew("厄尔庇斯", 961, ref SelectedAetherCurrents);
                ImGui.EndGroup();
                ImGui.EndGroup();

                // 右半边
                ImGui.SameLine();
                ImGui.BeginGroup();
                ImGui.BeginGroup();
                ImGui.AlignTextToFramePadding();
                ImGui.Text("萨维奈岛");
                ImGui.AlignTextToFramePadding();
                ImGui.Text("叹息海");
                ImGui.AlignTextToFramePadding();
                ImGui.Text("天外天垓");
                ImGui.EndGroup();

                ImGui.SameLine();
                ImGui.BeginGroup();
                DrawManuallySelectGroupNew("萨维奈岛", 957, ref SelectedAetherCurrents);
                DrawManuallySelectGroupNew("叹息海", 959, ref SelectedAetherCurrents);
                DrawManuallySelectGroupNew("天外天垓", 960, ref SelectedAetherCurrents);
                ImGui.EndGroup();
                ImGui.EndGroup();

                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    public void OverlayUI() { }

    private void DrawManuallySelectGroupOld(
        string territoryName, uint territoryID, ref Dictionary<uint, HashSet<AetherCurrent>> selectedCurrents)
    {
        ImGui.PushID(territoryName);
        for (var i = 3; i >= 0; i--)
        {
            var aetherCurrent =
                PresetAetherCurrentsData.FirstOrDefault(d => d.TerritoryID == territoryID && d.AetherCurrent.Index == i)
                                        .AetherCurrent;
            if (aetherCurrent == null) break;
            var decoBool = selectedCurrents[territoryID].Contains(aetherCurrent);
            if (ImGui.Checkbox($"###{territoryName}{i}", ref decoBool))
            {
                if (!selectedCurrents[territoryID].Remove(aetherCurrent))
                    selectedCurrents[territoryID].Add(aetherCurrent);
            }

            if (i != 0) ImGui.SameLine();
        }

        ImGui.PopID();
    }

    private void DrawManuallySelectGroupNew(
        string territoryName, uint territoryID, ref Dictionary<uint, HashSet<AetherCurrent>> selectedCurrents)
    {
        ImGui.PushID(territoryName);
        for (var i = 9; i >= 0; i--)
        {
            var aetherCurrent =
                PresetAetherCurrentsData.FirstOrDefault(d => d.TerritoryID == territoryID && d.AetherCurrent.Index == i)
                                        .AetherCurrent;
            if (aetherCurrent == null) break;
            var decoBool = selectedCurrents[territoryID].Contains(aetherCurrent);
            if (ImGui.Checkbox($"###{territoryName}{i}", ref decoBool))
            {
                if (!selectedCurrents[territoryID].Remove(aetherCurrent))
                    selectedCurrents[territoryID].Add(aetherCurrent);
            }

            if (i != 0) ImGui.SameLine();
        }

        ImGui.PopID();
    }

    private void OnZoneChanged(ushort territoryID)
    {
        MarkAetherCurrents(territoryID, false);
    }

    private void MarkAetherCurrents(ushort territoryID, bool isFirstPage)
    {
        if (!ValidTerritories.Contains(territoryID)) return;

        var waymarkIndexesLength = Enum.GetValues(typeof(FieldMarkerManager.FieldMarkerPoint)).Length;

        List<(uint TerritoryID, AetherCurrent AetherCurrent)> result =
            SelectedAetherCurrents.TryGetValue(Service.ClientState.TerritoryType, out var selectedResult) &&
            selectedResult.Any()
                ? selectedResult.Select(selected => ((uint)Service.ClientState.TerritoryType, selected)).ToList()
                : PresetAetherCurrentsData
                  .Where(i => i.TerritoryID == Service.ClientState.TerritoryType &&
                              (isFirstPage
                                   ? i.AetherCurrent.Index >= waymarkIndexesLength
                                   : i.AetherCurrent.Index < waymarkIndexesLength))
                  .Select(i => (i.TerritoryID, i.AetherCurrent))
                  .OrderBy(i => i.AetherCurrent.Index)
                  .ToList();


        var currentIndex = 0;

        foreach (var point in result)
        {
            if (currentIndex >= waymarkIndexesLength) break;

            var currentMarker = (FieldMarkerManager.FieldMarkerPoint)currentIndex;

            Service.Waymarks.Place(currentMarker, point.AetherCurrent.Position, true);
            currentIndex++;
        }

        if (currentIndex != 8)
        {
            for (; currentIndex < waymarkIndexesLength; currentIndex++)
                Service.Waymarks.Place((FieldMarkerManager.FieldMarkerPoint)currentIndex, Vector3.Zero, false);
        }
    }

    public void Uninit()
    {
        Service.ClientState.TerritoryChanged -= OnZoneChanged;
    }
}
