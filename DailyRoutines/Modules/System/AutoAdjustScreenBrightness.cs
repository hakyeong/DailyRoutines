using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
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
        public int BrightnessThreshold = 75;
        public int BrightnessMin = 30;
        public int BrightnessMax = 100;
        public int AdjustSpeed = 100;
    }

    private static CancellationTokenSource? CancellationTokenSource;

    private static int ScreenBrightness;
    private static double SceneBrightness;


    private static readonly ScreenHelper Screen = new();
    private static int OriginalBrightness = 100;

    private static Config? ModuleConfig;


    public override void Init()
    {
        ModuleConfig ??= LoadConfig<Config>() ?? new();

        var origBrightness = Screen.GetBrightness();
        OriginalBrightness = origBrightness + 10;

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

        ImGui.GetWindowDrawList().AddRect(ImGui.GetItemRectMin() - ImGuiHelpers.ScaledVector2(2f), ImGui.GetItemRectMax() + ImGuiHelpers.ScaledVector2(2f), ImGui.ColorConvertFloat4ToU32(ImGuiColors.DalamudWhite), 2f, ImDrawFlags.RoundCornersTop, 3f);

        ImGuiHelpers.ScaledDummy(5f);

        ImGui.SetNextItemWidth(100f * ImGuiHelpers.GlobalScale);
        ImGui.InputInt($"{Service.Lang.GetText("AutoAdjustScreenBrightness-Threshold")} (%)", ref ModuleConfig.BrightnessThreshold, 0, 0);
        if (ImGui.IsItemDeactivatedAfterEdit())
            SaveConfig(ModuleConfig);

        ImGuiOm.TooltipHover(Service.Lang.GetText("AutoAdjustScreenBrightness-ThresholdHelp"));

        ImGui.SetNextItemWidth(100f * ImGuiHelpers.GlobalScale);
        ImGui.InputInt($"{Service.Lang.GetText("AutoAdjustScreenBrightness-AdjustSpeed")} (%)", ref ModuleConfig.AdjustSpeed, 0, 0);
        if (ImGui.IsItemDeactivatedAfterEdit())
            SaveConfig(ModuleConfig);

        ImGui.SetNextItemWidth(100f * ImGuiHelpers.GlobalScale);
        ImGui.InputInt($"{Service.Lang.GetText("AutoAdjustScreenBrightness-BrightnessMin")} (%)", ref ModuleConfig.BrightnessMin, 0, 0);
        if (ImGui.IsItemDeactivatedAfterEdit())
            SaveConfig(ModuleConfig);

        ImGui.SetNextItemWidth(100f * ImGuiHelpers.GlobalScale);
        ImGui.InputInt($"{Service.Lang.GetText("AutoAdjustScreenBrightness-BrightnessOriginal")} (%)", ref OriginalBrightness, 0, 0);

        ImGuiOm.TooltipHover(Service.Lang.GetText("AutoAdjustScreenBrightness-BrightnessOriginalHelp"));
    }

    public static void BrightnessMonitor(CancellationToken token)
    {
        var currentBrightness = OriginalBrightness;
        var targetBrightness = OriginalBrightness;
        var stepSize = ModuleConfig.AdjustSpeed == 0 ? 1 : ModuleConfig.AdjustSpeed / 10 + 1;

        Task.Run(async () =>
        {
            while (true)
            {
                if (!WindowFunctions.ApplicationIsActivated())
                {
                    targetBrightness = OriginalBrightness;
                    await Task.Delay(100, token);
                    continue;
                }

                var bitmap = CaptureScreen();
                if (token.IsCancellationRequested)
                {
                    bitmap.Dispose();
                    return;
                }

                ScreenBrightness = Screen.GetBrightness();
                SceneBrightness = CalculateBrightPixelsPercentage(bitmap);

                bitmap.Dispose();

                targetBrightness = SceneBrightness > ModuleConfig.BrightnessThreshold ? 
                                       ModuleConfig.BrightnessMin : OriginalBrightness;

                await Task.Delay(100, token);
            }
        }, token);

        Task.Run(async () =>
        {
            while (true)
            {
                if (!WindowFunctions.ApplicationIsActivated())
                {
                    targetBrightness = OriginalBrightness;
                    Screen.SetBrightness((byte)targetBrightness);
                    await Task.Delay(500, token);
                    continue;
                }

                if (currentBrightness != targetBrightness)
                {
                    var brightnessAdjustment = (targetBrightness > currentBrightness) ? stepSize : -stepSize;
                    currentBrightness = (byte)Math.Max(0, Math.Min(255, currentBrightness + brightnessAdjustment));
                    Screen.SetBrightness((byte)currentBrightness);
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

        var brightPixels = 0;
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

                var brightness = ((GammaCorrect(red) * 0.299f) + (GammaCorrect(green) * 0.587f) +
                                  (GammaCorrect(blue) * 0.114f)) / 255;

                if (brightness > 0.65) brightPixels++;
            }
        }

        return (double)brightPixels / totalPixels * 100;
    }

    private static float GammaCorrect(float colorValue)
    {
        Service.GameConfig.TryGet(SystemConfigOption.Gamma, out uint gammaPercentage);

        const float minGamma = 1.8f;
        const float maxGamma = 2.4f;
        var gamma = minGamma + ((maxGamma - minGamma) * gammaPercentage / 100.0f);

        var inverseGamma = 1.0f / gamma;
        return (float)Math.Pow(colorValue / 255.0, inverseGamma) * 255;
    }


    public override void Uninit()
    {
        base.Uninit();

        CancellationTokenSource?.Cancel();
        CancellationTokenSource?.Dispose();
        CancellationTokenSource = null;

        Screen.SetBrightness((byte)OriginalBrightness);
    }

    private class ScreenHelper
    {
        // 有效亮度值
        private readonly byte[] ValidBrightnessLevels;

        // Namespace
        private readonly ManagementScope Namescope = new("root\\WMI");
        // 查询
        private readonly SelectQuery Query = new("WmiMonitorBrightness");
        private readonly SelectQuery QueryMethods = new("WmiMonitorBrightnessMethods");

        public bool IsSupported { get; set; }

        public ScreenHelper()
        {
            ValidBrightnessLevels = GetBrightnessLevels();
            IsSupported = ValidBrightnessLevels.Length != 0;
        }

        public void IncreaseBrightness()
        {
            if (IsSupported) FindAndSetupBrightness(GetBrightness() + 10);
        }

        public void DecreaseBrightness()
        {
            if (IsSupported) FindAndSetupBrightness(GetBrightness() - 10);
        }

        /// <summary>
        /// 获取当前系统亮度
        /// </summary>
        /// <returns></returns>
        public int GetBrightness()
        {
            using ManagementObjectSearcher searcher = new(Namescope, Query);
            using var objCollection = searcher.Get();

            byte curBrightness = 0;
            foreach (var o in objCollection)
            {
                var obj = (ManagementObject)o;
                curBrightness = (byte)obj.GetPropertyValue("CurrentBrightness");
                break;
            }

            return curBrightness;
        }

        /// <summary>
        /// 将输入亮度转为系统识别用 byte
        /// </summary>
        /// <param name="iPercent"></param>
        private void FindAndSetupBrightness(int iPercent)
        {
            iPercent = iPercent switch
            {
                < 0 => 0,
                > 100 => 100,
                _ => iPercent
            };

            if (iPercent <= ValidBrightnessLevels[^1])
            {
                byte level = 100;
                foreach (var item in ValidBrightnessLevels)
                    // 找到数组中与传入的 iPercent 接近的一项
                    if (item >= iPercent)
                    {
                        level = item;
                        break;
                    }

                SetBrightness(level);
            }
        }

        /// <summary>
        /// 设置亮度
        /// </summary>
        /// <param name="targetBrightness"></param>
        public void SetBrightness(byte targetBrightness)
        {
            using var searcher = new ManagementObjectSearcher(Namescope, QueryMethods);
            using var objectCollection = searcher.Get();
            foreach (var o in objectCollection)
            {
                var mObj = (ManagementObject)o;
                mObj.InvokeMethod("WmiSetBrightness", [uint.MaxValue, targetBrightness]);
                break;
            }
        }

        private byte[] GetBrightnessLevels()
        {
            using ManagementObjectSearcher mos = new(Namescope, Query);

            var bLevels = Array.Empty<byte>();
            try
            {
                using var moc = mos.Get();
                foreach (var managementBaseObject in moc)
                {
                    var o = (ManagementObject)managementBaseObject;
                    bLevels = (byte[])o.GetPropertyValue("Level");
                    break;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
            }

            return bLevels;
        }
    }
}
