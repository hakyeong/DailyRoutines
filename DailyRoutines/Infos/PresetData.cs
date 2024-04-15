using DailyRoutines.Managers;
using System.Collections.Generic;
using System.Linq;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using Microsoft.Extensions.Caching.Memory;
using Action = Lumina.Excel.GeneratedSheets.Action;
using Status = Lumina.Excel.GeneratedSheets.Status;

namespace DailyRoutines.Infos;

public static class LuminaCache
{
    private static readonly MemoryCache Cache = new(new MemoryCacheOptions());

    private static string GetSheetCacheKey<T>() where T : ExcelRow => $"ExcelSheet_{typeof(T).FullName}";
    private static string GetRowCacheKey<T>(uint rowID) where T : ExcelRow => $"ExcelRow_{typeof(T).FullName}_{rowID}";

    public static ExcelSheet<T>? Get<T>() where T : ExcelRow => TryGetAndCacheSheet<T>(out var sheet) ? sheet : null;

    public static bool TryGet<T>(out ExcelSheet<T> sheet) where T : ExcelRow => TryGetAndCacheSheet(out sheet);

    public static T? GetRow<T>(uint rowID) where T : ExcelRow => TryGetRow<T>(rowID, out var item) ? item : null;

    public static bool TryGetRow<T>(uint rowID, out T? item) where T : ExcelRow
    {
        if (!TryGetAndCacheSheet<T>(out var sheet))
        {
            item = null;
            return false;
        }

        var rowCacheKey = GetRowCacheKey<T>(rowID);
        item = Cache.Get<T>(rowCacheKey);
        if (item != null)
        {
            return true;
        }

        item = sheet.GetRow(rowID);
        if (item != null)
        {
            Cache.Set(rowCacheKey, item);
        }

        return item != null;
    }

    private static bool TryGetAndCacheSheet<T>(out ExcelSheet<T>? sheet) where T : ExcelRow
    {
        var cacheKey = GetSheetCacheKey<T>();
        sheet = Cache.Get<ExcelSheet<T>>(cacheKey);

        if (sheet == null)
        {
            sheet = Service.Data.GetExcelSheet<T>();
            if (sheet != null) Cache.Set(cacheKey, sheet);
        }

        return sheet != null;
    }

    public static void ClearCache() => Cache.Clear();
}

public class PresetData
{
    public Dictionary<uint, Action>? PlayerActions { get; private set; }
    public Dictionary<uint, Status>? Statuses { get; private set; }
    public Dictionary<uint, ContentFinderCondition>? Contents { get; private set; }
    public Dictionary<uint, Item>? Gears { get; private set; }
    public Dictionary<uint, Item>? Dyes { get; private set; } // 不包含特制

    public PresetData()
    {
        PlayerActions ??= LuminaCache.Get<Action>()
                                 .Where(x => x.ClassJobCategory.Row > 0 && x.ActionCategory.Row <= 4 && x.RowId > 8 &&
                                             !string.IsNullOrWhiteSpace(x.Name.ExtractText()))
                                 .ToDictionary(x => x.RowId, x => x);

        Statuses ??= LuminaCache.Get<Status>()
                                .Where(x => !string.IsNullOrWhiteSpace(x.Name.ExtractText()))
                                .ToDictionary(x => x.RowId, x => x);

        Contents ??= LuminaCache.Get<ContentFinderCondition>()
                                .Where(x => !x.Name.ToString().IsNullOrEmpty())
                                .DistinctBy(x => x.TerritoryType.Row)
                                .ToDictionary(x => x.TerritoryType.Row, x => x);

        Gears ??= LuminaCache.Get<Item>()
                             .Where(x => x.EquipSlotCategory.Value.RowId != 0)
                             .DistinctBy(x => x.RowId)
                             .ToDictionary(x => x.RowId, x => x);

        Dyes ??= LuminaCache.Get<StainTransient>()
                            .Where(x => x.Item1.Value != null)
                            .ToDictionary(x => x.RowId, x => x.Item1.Value)!;
    }

    public bool TryGetPlayerActions(uint rowID, out Action action)
        => PlayerActions.TryGetValue(rowID, out action);

    public bool TryGetStatus(uint rowID, out Status status)
        => Statuses.TryGetValue(rowID, out status);

    public bool TryGetContent(uint rowID, out ContentFinderCondition content)
        => Contents.TryGetValue(rowID, out content);

    public bool TryGetGear(uint rowID, out Item item)
        => Gears.TryGetValue(rowID, out item);

    public bool TryGetStain(uint rowID, out Item item)
        => Dyes.TryGetValue(rowID, out item);
}
