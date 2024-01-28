using DailyRoutines.Managers;
using System.Collections.Generic;
using System.Linq;
using Lumina.Excel.GeneratedSheets;

namespace DailyRoutines.Infos;

public class ExcelGameData
{
    public Dictionary<string, uint> ItemNames { get; private set; } = new();
    public Dictionary<uint, Action>? Actions { get; private set; } = new();

    public ExcelGameData()
    {
        ItemNames = Service.Data.GetExcelSheet<Item>()
                           .Where(x => !string.IsNullOrEmpty(x.Name.RawString))
                           .DistinctBy(x => x.Name.RawString)
                           .ToDictionary(x => x.Name.RawString, x => x.RowId);

        Actions = Service.Data.GetExcelSheet<Action>()
                         ?.Where(x => x.ClassJobCategory.Row > 0 && x.ActionCategory.Row <= 4 && x.RowId > 8)
                         .ToDictionary(x => x.RowId, x => x);
    }
}
