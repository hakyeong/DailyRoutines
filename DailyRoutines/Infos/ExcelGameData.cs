using DailyRoutines.Managers;
using System.Collections.Generic;
using System.Linq;
using Lumina.Excel.GeneratedSheets;

namespace DailyRoutines.Infos;

public class ExcelGameData
{
    public Dictionary<string, uint> ItemNames { get; private set; } = new();
    public Dictionary<uint, string> LeveQuests { get; private set; } = new();

    public ExcelGameData()
    {
        ItemNames = Service.Data.GetExcelSheet<Item>()
                           .Where(x => !string.IsNullOrEmpty(x.Name.RawString))
                           .DistinctBy(x => x.Name.RawString)
                           .ToDictionary(x => x.Name.RawString, x => x.RowId);

        LeveQuests = Service.Data.Excel.GetSheet<Leve>()
                            .Where(x => !string.IsNullOrEmpty(x.Name.RawString))
                            .ToDictionary(x => x.RowId, x => x.Name.RawString);
    }
}
