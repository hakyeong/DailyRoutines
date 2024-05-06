using System.Timers;
using DailyRoutines.Infos;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Timer = System.Timers.Timer;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoAntiAfkTitle", "AutoAntiAfkDescription", ModuleCategories.»ù´¡)]
public class AutoAntiAfk : DailyModuleBase
{
    private static Timer? AfkTimer;

    public override void Init()
    {
        AfkTimer ??= new Timer(10000);
        AfkTimer.Elapsed += OnAfkStateCheck;
        AfkTimer.AutoReset = true;
        AfkTimer.Enabled = true;
    }

    private static unsafe void OnAfkStateCheck(object? sender, ElapsedEventArgs e)
    {
        var inputTimerModule = InputTimerModule.Instance();
        if (inputTimerModule != null)
        {
            inputTimerModule->AfkTimer = 0;
            inputTimerModule->ContentInputTimer = 0;
            inputTimerModule->InputTimer = 0;
            inputTimerModule->Unk1C = 0;
        }
    }

    public override void Uninit()
    {
        AfkTimer?.Stop();
        if (AfkTimer != null) AfkTimer.Elapsed -= OnAfkStateCheck;
        AfkTimer?.Dispose();
        AfkTimer = null;

        base.Uninit();
    }
}
