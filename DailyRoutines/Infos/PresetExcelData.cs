using DailyRoutines.Managers;
using System.Collections.Generic;
using System.Linq;
using Lumina.Excel.GeneratedSheets;

namespace DailyRoutines.Infos;

public class PresetExcelData
{
    public Dictionary<uint, Action>? PlayerActions { get; private set; }
    public Dictionary<uint, ContentFinderCondition>? Contents { get; private set; }

    public PresetExcelData()
    {
        PlayerActions ??= Service.Data.GetExcelSheet<Action>()
                         ?.Where(x => x.ClassJobCategory.Row > 0 && x.ActionCategory.Row <= 4 && x.RowId > 8)
                         .ToDictionary(x => x.RowId, x => x);

        Contents ??= Service.Data.GetExcelSheet<ContentFinderCondition>()
                          .Where(x => !x.Name.ToString().IsNullOrEmpty())
                          .DistinctBy(x => x.TerritoryType.Row)
                          .ToDictionary(x => x.TerritoryType.Row, x => x);
    }
}
