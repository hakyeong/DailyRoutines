using System.Timers;
using DailyRoutines.Infos;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoAntiAfkTitle", "AutoAntiAfkDescription", ModuleCategories.系统)]
public class AutoAntiAfk : DailyModuleBase
{
    private static Timer? AfkTimer;

    public override void Init()
    {
        AfkTimer ??= new Timer(10000) { AutoReset = true, Enabled = true };
        AfkTimer.Elapsed += ResetAfkTimers;
    }

    private static unsafe void ResetAfkTimers(object? sender, ElapsedEventArgs e)
    {
        var timerModule = InputTimerModule.Instance();
        if (timerModule != null)
            timerModule->AfkTimer = timerModule->ContentInputTimer = timerModule->InputTimer = timerModule->Unk1C = 0;
    }

    public override void Uninit()
    {
        if (AfkTimer != null)
        {
            AfkTimer.Stop();
            AfkTimer.Elapsed -= ResetAfkTimers;
            AfkTimer.Dispose();
        }
        AfkTimer = null;
    }
}
