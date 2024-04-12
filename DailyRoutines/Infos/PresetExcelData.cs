using DailyRoutines.Managers;
using System.Collections.Generic;
using System.Linq;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using Status = Lumina.Excel.GeneratedSheets.Status;

namespace DailyRoutines.Infos;

public class PresetExcelData
{
    public Dictionary<uint, Action>? PlayerActions { get; private set; }
    public Dictionary<uint, Status>? Statuses { get; private set; }
    public Dictionary<uint, ContentFinderCondition>? Contents { get; private set; }
    public Dictionary<uint, Item>? Gears { get; private set; }

    public PresetExcelData()
    {
        PlayerActions ??= Service.Data.GetExcelSheet<Action>()
                                 .Where(x => x.ClassJobCategory.Row > 0 && x.ActionCategory.Row <= 4 && x.RowId > 8 &&
                                             !string.IsNullOrWhiteSpace(x.Name.ExtractText()))
                                 .ToDictionary(x => x.RowId, x => x);

        Statuses ??= Service.Data.GetExcelSheet<Status>()
                            .Where(x => !string.IsNullOrWhiteSpace(x.Name.ExtractText()))
                            .ToDictionary(x => x.RowId, x => x);

        Contents ??= Service.Data.GetExcelSheet<ContentFinderCondition>()
                            .Where(x => !x.Name.ToString().IsNullOrEmpty())
                            .DistinctBy(x => x.TerritoryType.Row)
                            .ToDictionary(x => x.TerritoryType.Row, x => x);

        Gears ??= Service.Data.GetExcelSheet<Item>()
                                  .Where(x => x.EquipSlotCategory.Value.RowId != 0)
                                  .DistinctBy(x => x.RowId)
                                  .ToDictionary(x => x.RowId, x => x);
    }
}
