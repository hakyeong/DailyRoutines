using System.Collections.Generic;
using System.Linq;
using DailyRoutines.Helpers;
using Lumina.Excel.GeneratedSheets;
using Action = Lumina.Excel.GeneratedSheets.Action;
using Status = Lumina.Excel.GeneratedSheets.Status;

namespace DailyRoutines.Infos;

public class PresetData
{
    public static Dictionary<uint, Action>? PlayerActions { get; private set; }
    public static Dictionary<uint, Status>? Statuses { get; private set; }
    public static Dictionary<uint, ContentFinderCondition>? Contents { get; private set; }
    public static Dictionary<uint, Item>? Gears { get; private set; }
    public static Dictionary<uint, Item>? Dyes { get; private set; } // 不包含特制
    public static Dictionary<uint, World>? CNWorlds { get; private set; }
    public static Dictionary<uint, TerritoryType>? Zones { get; private set; }

    public static void Init()
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

        CNWorlds ??= LuminaCache.Get<World>()
                                .Where(x => x.DataCenter.Value.Region == 5 && !string.IsNullOrWhiteSpace(x.Name.RawString) && !string.IsNullOrWhiteSpace(x.InternalName.RawString) && IsChineseString(x.Name.RawString))
                                .ToDictionary(x => x.RowId, x => x);

        Zones ??= LuminaCache.Get<TerritoryType>()
                             .Where(x => !(string.IsNullOrWhiteSpace(x.Name.RawString) || x.PlaceNameIcon <= 0 ||
                                           x.PlaceNameRegionIcon <= 0))
                             .ToDictionary(x => x.RowId, x => x);
    }

    public static bool TryGetPlayerActions(uint rowID, out Action action)
        => PlayerActions.TryGetValue(rowID, out action);

    public static bool TryGetStatus(uint rowID, out Status status)
        => Statuses.TryGetValue(rowID, out status);

    public static bool TryGetContent(uint rowID, out ContentFinderCondition content)
        => Contents.TryGetValue(rowID, out content);

    public static bool TryGetGear(uint rowID, out Item item)
        => Gears.TryGetValue(rowID, out item);

    public static bool TryGetStain(uint rowID, out Item item)
        => Dyes.TryGetValue(rowID, out item);

    public static bool TryGetCNWorld(uint rowID, out World world)
        => CNWorlds.TryGetValue(rowID, out world);

    public static bool TryGetZone(uint rowID, out TerritoryType zone)
        => Zones.TryGetValue(rowID, out zone);
}
