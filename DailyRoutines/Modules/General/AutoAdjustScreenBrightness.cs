using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Config;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using ECommons.Interop;
using ImGuiNET;
using Task = System.Threading.Tasks.Task;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoAdjustScreenBrightnessTitle", "AutoAdjustScreenBrightnessDescription", ModuleCategories.一般)]
public class AutoAdjustScreenBrightness : DailyModuleBase
{
    private class Config : ModuleConfiguration
    {
        // 百分比
        public int BrightnessThreshold = 55;
        public int BrightnessThresholdContent = 65;
        public int BrightnessMin = 30;
        public int AdjustSpeed = 50;
    }

    private static int ScreenBrightness;
    private static double SceneBrightness;

    private static readonly ScreenHelper Screen = new();
    private static int OriginalBrightness = 100;

    private static CancellationTokenSource? CancellationTokenSource;
    private static Config ModuleConfig = null!;


    public override void Init()
    {
        ModuleConfig = LoadConfig<Config>() ?? new();

        var origBrightness = Screen.GetCurrentBrightness();
        OriginalBrightness = origBrightness + 10; // Windows 亮度设置的问题

        CancellationTokenSource ??= new();
        BrightnessMonitor(CancellationTokenSource.Token);
    }

    public override void ConfigUI()
    {
        ImGui.BeginGroup();
        ImGui.Text($"{Service.Lang.GetText("AutoAdjustScreenBrightness-CurrentScreenBrightness")}: {ScreenBrightness}%%");

        ImGui.SameLine();
        ImGui.Text($"{Service.Lang.GetText("AutoAdjustScreenBrightness-CurrentSceneBrightness")}: {Math.Round(SceneBrightness, 2)}%%");
        ImGui.EndGroup();

        ImGui.GetWindowDrawList().AddRect(ImGui.GetItemRectMin() - ImGuiHelpers.ScaledVector2(2f), ImGui.GetItemRectMax() + ImGuiHelpers.ScaledVector2(2f), ImGui.ColorConvertFloat4ToU32(ImGuiColors.DalamudWhite), 2f, ImDrawFlags.RoundCornersAll, 3f);

        ImGuiHelpers.ScaledDummy(5f);

        ImGui.BeginGroup();
        ImGui.SetNextItemWidth(150f * ImGuiHelpers.GlobalScale);
        ImGui.SliderInt($"{Service.Lang.GetText("AutoAdjustScreenBrightness-Threshold")} (%)",
                        ref ModuleConfig.BrightnessThreshold, -10, 100);
        if (ImGui.IsItemDeactivatedAfterEdit())
            SaveConfig(ModuleConfig);

        ImGui.SetNextItemWidth(150f * ImGuiHelpers.GlobalScale);
        ImGui.SliderInt($"{Service.Lang.GetText("AutoAdjustScreenBrightness-ThresholdContent")} (%)",
                        ref ModuleConfig.BrightnessThresholdContent, -10, 100);
        if (ImGui.IsItemDeactivatedAfterEdit())
            SaveConfig(ModuleConfig);
        ImGui.EndGroup();

        ImGuiOm.TooltipHover(Service.Lang.GetText("AutoAdjustScreenBrightness-ThresholdHelp"));

        ImGui.SetNextItemWidth(150f * ImGuiHelpers.GlobalScale);
        ImGui.SliderInt($"{Service.Lang.GetText("AutoAdjustScreenBrightness-AdjustSpeed")} (%)",
                        ref ModuleConfig.AdjustSpeed, 1, 200);
        if (ImGui.IsItemDeactivatedAfterEdit())
            SaveConfig(ModuleConfig);

        ImGuiOm.TooltipHover(Service.Lang.GetText("AutoAdjustScreenBrightness-AdjustSpeedHelp"));

        ImGui.SetNextItemWidth(150f * ImGuiHelpers.GlobalScale);
        ImGui.SliderInt($"{Service.Lang.GetText("AutoAdjustScreenBrightness-BrightnessMin")} (%)",
                        ref ModuleConfig.BrightnessMin, -10, 110);
        if (ImGui.IsItemDeactivatedAfterEdit())
            SaveConfig(ModuleConfig);

        ImGuiOm.TooltipHover(Service.Lang.GetText("AutoAdjustScreenBrightness-BrightnessMinHelp"));

        ImGui.SetNextItemWidth(150f * ImGuiHelpers.GlobalScale);
        ImGui.SliderInt($"{Service.Lang.GetText("AutoAdjustScreenBrightness-BrightnessOriginal")} (%)",
                        ref OriginalBrightness, -10, 110);

        ImGuiOm.TooltipHover(Service.Lang.GetText("AutoAdjustScreenBrightness-BrightnessOriginalHelp"));
    }

    public static void BrightnessMonitor(CancellationToken token)
    {
        // 缓冲区间
        const int hysteresis = 5;
        // 步幅
        var stepSize = ModuleConfig.AdjustSpeed == 0 ? 1 : (ModuleConfig.AdjustSpeed / 10) + 1;
        // 是否已恢复原始亮度
        var haveRestored = false;

        var currentBrightness = OriginalBrightness;
        var targetBrightness = OriginalBrightness;
        var lastSignificantBrightness = OriginalBrightness;

        Task.Run(async () =>
        {
            while (true)
            {
                if (!WindowFunctions.ApplicationIsActivated())
                {
                    await Task.Delay(100, token);
                    continue;
                }

                using var bitmap = CaptureScreen();
                if (token.IsCancellationRequested)
                {
                    return;
                }

                ScreenBrightness = Screen.GetCurrentBrightness();
                SceneBrightness = CalculateBrightPixelsPercentage(bitmap);

                // 防抖动
                if (Math.Abs(SceneBrightness - lastSignificantBrightness) > hysteresis)
                {
                    targetBrightness = SceneBrightness > (Flags.BoundByDuty() ? ModuleConfig.BrightnessThresholdContent : ModuleConfig.BrightnessThreshold) ? ModuleConfig.BrightnessMin : OriginalBrightness;
                    lastSignificantBrightness = (int)SceneBrightness;
                }

                await Task.Delay(100, token);
            }
        }, token);

        Task.Run(async () =>
        {
            while (true)
            {
                if (!WindowFunctions.ApplicationIsActivated())
                {
                    if (!haveRestored)
                    {
                        targetBrightness = OriginalBrightness;
                        Screen.SetBrightness(targetBrightness);
                        haveRestored = true;
                    }
                    await Task.Delay(500, token);
                    continue;
                }

                if (haveRestored)
                {
                    Screen.SetBrightness(targetBrightness);
                    haveRestored = false;
                }

                if (currentBrightness != targetBrightness)
                {
                    var brightnessAdjustment = (targetBrightness > currentBrightness) ? stepSize : -stepSize;
                    currentBrightness = Math.Max(0, Math.Min(255, currentBrightness + brightnessAdjustment));
                    Screen.SetBrightness(currentBrightness);
                }

                await Task.Delay(100, token);
            }
        }, token);
    }

    public static Bitmap CaptureScreen()
    {
        var bounds = System.Windows.Forms.Screen.GetBounds(Point.Empty);
        var bitmap = new Bitmap(bounds.Width, bounds.Height);

        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);

        return bitmap;
    }

    public static double CalculateBrightPixelsPercentage(Bitmap bitmap)
    {
        var marginWidth = bitmap.Width / 4;
        var marginHeight = bitmap.Height / 4;
        var centerWidth = bitmap.Width / 2;
        var centerHeight = bitmap.Height / 2;

        var centerRect = new Rectangle(marginWidth, marginHeight, centerWidth, centerHeight);
        var bitmapData = bitmap.LockBits(centerRect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        var bytesPerPixel = Image.GetPixelFormatSize(bitmapData.PixelFormat) / 8;
        var byteCount = bitmapData.Stride * bitmapData.Height;
        var pixels = new byte[byteCount];
        Marshal.Copy(bitmapData.Scan0, pixels, 0, byteCount);
        bitmap.UnlockBits(bitmapData);

        var brightPixels = 0D;
        var totalPixels = centerWidth * centerHeight;
        for (var y = 0; y < centerHeight; y++)
        {
            var currentLine = y * bitmapData.Stride;
            for (var x = 0; x < centerWidth; x++)
            {
                var xIndex = currentLine + (x * bytesPerPixel);
                var blue = pixels[xIndex];
                var green = pixels[xIndex + 1];
                var red = pixels[xIndex + 2];

                var perceivedBrightness = (0.299 * red + 0.587 * green + 0.114 * blue) / 255.0;

                // 接近白色的像素标记为刺眼
                if (perceivedBrightness > 0.8)
                {
                    perceivedBrightness = 1.0;
                    brightPixels += 0.5;
                }

                if (perceivedBrightness > 0.75)
                    brightPixels++;
            }
        }

        brightPixels = Math.Min(totalPixels, brightPixels);

        return brightPixels / totalPixels * 100;
    }

    public override void Uninit()
    {
        base.Uninit();

        CancellationTokenSource?.Cancel();
        CancellationTokenSource?.Dispose();
        CancellationTokenSource = null;

        Screen.SetBrightness(OriginalBrightness);
    }

    private class ScreenHelper
    {
        private readonly byte[] validBrightnessLevels;
        private readonly ManagementScope scope = new("root\\WMI");
        private readonly SelectQuery queryBrightness = new("WmiMonitorBrightness");
        private readonly SelectQuery queryMethods = new("WmiMonitorBrightnessMethods");

        public bool IsSupported { get; private set; }

        public ScreenHelper()
        {
            validBrightnessLevels = GetBrightnessLevels();
            IsSupported = validBrightnessLevels.Length > 0;
        }

        public void AdjustBrightness(int change)
        {
            if (IsSupported)
                SetupBrightness(GetCurrentBrightness() + change);
        }

        public int GetCurrentBrightness()
        {
            using var searcher = new ManagementObjectSearcher(scope, queryBrightness);
            foreach (var o in searcher.Get())
            {
                var obj = (ManagementObject)o;
                return (byte)obj.GetPropertyValue("CurrentBrightness");
            }
            return 0;
        }

        private void SetupBrightness(int targetPercent)
        {
            targetPercent = Math.Clamp(targetPercent, 0, 100);
            var nearestValidBrightness = validBrightnessLevels.FirstOrDefault(level => level >= targetPercent);
            if (nearestValidBrightness > 0)
                SetBrightness(nearestValidBrightness);
        }

        public void SetBrightness(int brightness) => SetBrightness((byte)brightness);

        public void SetBrightness(byte brightness)
        {
            using var searcher = new ManagementObjectSearcher(scope, queryMethods);
            foreach (var o in searcher.Get())
            {
                var obj = (ManagementObject?)o;
                obj.InvokeMethod("WmiSetBrightness", [uint.MaxValue, brightness]);
                break;
            }
        }

        private byte[] GetBrightnessLevels()
        {
            using var searcher = new ManagementObjectSearcher(scope, queryBrightness);
            try
            {
                foreach (var o in searcher.Get())
                {
                    var obj = (ManagementObject)o;
                    return (byte[])obj.GetPropertyValue("Level");
                }
            }
            catch (Exception ex)
            {
                Service.Log.Error(ex.Message);
            }
            return [];
        }
    }
}
