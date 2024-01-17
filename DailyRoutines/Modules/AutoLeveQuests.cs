using System.Collections.Generic;
using System.Linq;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.GeneratedSheets;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoLeveQuestsTitle", "AutoLeveQuestsDescription", ModuleCategories.General)]
public class AutoLeveQuests : IDailyModule
{
    public bool Initialized { get; set; }

    public void Init()
    {
        Initialized = true;
    }

    public void UI()
    {

    }

    public static unsafe void QuestHandler()
    {
        var manager = QuestManager.Instance();
        Service.Log.Debug(Service.ExcelData.LeveQuests.Count.ToString());
        foreach (var leve in manager->LeveQuestsSpan)
        {
            Service.Log.Debug(Service.ExcelData.LeveQuests[leve.LeveId]);
        }
    }

    public void Uninit()
    {
        Initialized = false;
    }
}
