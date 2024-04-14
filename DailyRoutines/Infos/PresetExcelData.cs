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
    public Dictionary<uint, Item>? Dyes { get; private set; } // 不包含特制

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

        Dyes ??= Service.Data.GetExcelSheet<StainTransient>()
                        .Where(x => x.Item1.Value != null)
                        .ToDictionary(x => x.RowId, x => x.Item1.Value)!;
    }

    public static bool TryGet<T>(uint rowID, out T item) where T : ExcelRow
    {
        item = Service.Data.GetExcelSheet<T>().GetRow(rowID);
        return item != null;
    }
    public bool TryGetPlayerActions(uint actionID, out Action action)
        => PlayerActions.TryGetValue(actionID, out action);

    public bool TryGetStatus(uint actionID, out Status status)
        => Statuses.TryGetValue(actionID, out status);

    public bool TryGetContent(uint actionID, out ContentFinderCondition content)
        => Contents.TryGetValue(actionID, out content);

    public bool TryGetGear(uint actionID, out Item item)
        => Gears.TryGetValue(actionID, out item);

    public bool TryGetStain(uint actionID, out Item item)
        => Dyes.TryGetValue(actionID, out item);
}
