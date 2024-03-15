using System;
using System.Threading;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace DailyRoutines.Modules;

// 完全来自 STP 的 AutoRefreshMarketPrices, 作者为: Chalkos
[ModuleDescription("AutoRefreshMarketSearchResultTitle", "AutoRefreshMarketSearchResultDescription", ModuleCategories.Base)]
public unsafe class AutoRefreshMarketSearchResult : DailyModuleBase
{
    private delegate long HandlePricesDelegate(
        void* unk1, void* unk2, void* unk3, void* unk4, void* unk5, void* unk6,
        void* unk7);

    [Signature("E8 ?? ?? ?? ?? 8B 5B 04 85 DB", DetourName = nameof(HandlePricesDetour))]
    private Hook<HandlePricesDelegate>? HandlePricesHook;

    private nint waitMessageCodeChangeAddress = nint.Zero;
    private byte[] waitMessageCodeOriginalBytes = new byte[5];
    private bool waitMessageCodeError;

    private int failCount;
    private int maxFailCount;

    private CancellationTokenSource? cancelSource;

    public override void Init()
    {
        Service.Hook.InitializeFromAttributes(this);
        HandlePricesHook?.Enable();

        waitMessageCodeError = false;
        waitMessageCodeChangeAddress =
            Service.SigScanner.ScanText(
                "BA ?? ?? ?? ?? E8 ?? ?? ?? ?? 4C 8B C0 BA ?? ?? ?? ?? 48 8B CE E8 ?? ?? ?? ?? 45 33 C9");
        if (SafeMemory.ReadBytes(waitMessageCodeChangeAddress, 5, out waitMessageCodeOriginalBytes))
        {
            if (!SafeMemory.WriteBytes(waitMessageCodeChangeAddress, [0xBA, 0xB9, 0x1A, 0x00, 0x00]))
            {
                waitMessageCodeError = true;
                Service.Log.Error("Failed to write new instruction");
            }
        }
        else
        {
            waitMessageCodeError = true;
            Service.Log.Error("Failed to read original instruction");
        }
    }

    private long HandlePricesDetour(void* unk1, void* unk2, void* unk3, void* unk4, void* unk5, void* unk6, void* unk7)
    {
        cancelSource?.Cancel();
        cancelSource?.Dispose();
        cancelSource = new CancellationTokenSource();

        var result = HandlePricesHook.Original.Invoke(unk1, unk2, unk3, unk4, unk5, unk6, unk7);

        if (result != 1)
        {
            maxFailCount = Math.Max(++failCount, maxFailCount);
            Service.Framework.RunOnTick(() =>
            {
                if (TryGetAddonByName<AddonItemSearchResult>("ItemSearchResult", out var addonItemSearchResult)
                    && AddonItemSearchResultThrottled(addonItemSearchResult))
                {
                    Service.Framework.RunOnTick(RefreshPrices, TimeSpan.FromSeconds(2f + (0.5f * maxFailCount - 1)),
                                                0, cancelSource.Token);
                }
            });
        }
        else
            failCount = Math.Max(0, maxFailCount - 1);

        return result;
    }

    private static void RefreshPrices()
    {
        if (!TryGetAddonByName<AddonItemSearchResult>("ItemSearchResult", out var addonItemSearchResult)) return;
        if (!AddonItemSearchResultThrottled(addonItemSearchResult)) return;
        AgentManager.SendEvent(AgentId.ItemSearch, 2, 0, 0);
    }

    private static bool AddonItemSearchResultThrottled(AddonItemSearchResult* addon)
    {
        return addon != null
               && addon->ErrorMessage != null
               && addon->ErrorMessage->AtkResNode.IsVisible
               && addon->HitsMessage != null
               && !addon->HitsMessage->AtkResNode.IsVisible;
    }

    public override void Uninit()
    {
        if (!waitMessageCodeError &&
            !SafeMemory.WriteBytes(waitMessageCodeChangeAddress, waitMessageCodeOriginalBytes))
            Service.Log.Error("Failed to write original instruction");

        HandlePricesHook?.Dispose();
        cancelSource?.Cancel();
        cancelSource?.Dispose();

        base.Uninit();
    }
}
