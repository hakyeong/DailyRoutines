using System;
using System.Collections.Generic;
using System.Linq;
using DailyRoutines.Helpers;
using Lumina.Excel.GeneratedSheets;
using Action = Lumina.Excel.GeneratedSheets.Action;
using Status = Lumina.Excel.GeneratedSheets.Status;

namespace DailyRoutines.Infos;

public class PresetData
{
    public static Dictionary<uint, Action>                 PlayerActions => playerActions.Value;
    public static Dictionary<uint, Status>                 Statuses      => statuses.Value;
    public static Dictionary<uint, ContentFinderCondition> Contents      => contents.Value;
    public static Dictionary<uint, Item>                   Gears         => gears.Value;
    public static Dictionary<uint, Item>                   Dyes          => dyes.Value;
    public static Dictionary<uint, World>                  CNWorlds      => cnWorlds.Value;
    public static Dictionary<uint, TerritoryType>          Zones         => zones.Value;
    public static Dictionary<uint, Mount>                  Mounts        => mounts.Value;
    public static Dictionary<uint, Item>                   Food          => food.Value;

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

    public static bool TryGetMount(uint rowID, out Mount mount)
        => Mounts.TryGetValue(rowID, out mount);

    public static bool TryGetFood(uint rowID, out Item foodItem)
        => Food.TryGetValue(rowID, out foodItem);

    #region Lazy
    private static readonly Lazy<Dictionary<uint, Action>> playerActions =
        new(() => LuminaCache.Get<Action>()
                             .Where(x => x.ClassJob.Value != null && x.Range != -1 && x.Icon != 0 &&
                                         !string.IsNullOrWhiteSpace(x.Name.RawString))
                             .Where(x => x is
                             {
                                 IsPlayerAction: false,
                                 ClassJobLevel: > 0,
                             }
                                             or { IsPlayerAction: true })
                             .OrderBy(x => x.ClassJob.Row)
                             .ThenBy(x => x.ClassJobLevel)
                             .ToDictionary(x => x.RowId, x => x));

    private static readonly Lazy<Dictionary<uint, Status>> statuses =
        new(() => LuminaCache.Get<Status>()
                             .Where(x => !string.IsNullOrWhiteSpace(x.Name.RawString))
                             .ToDictionary(x => x.RowId, x => x));

    private static readonly Lazy<Dictionary<uint, ContentFinderCondition>> contents =
        new(() => LuminaCache.Get<ContentFinderCondition>()
                             .Where(x => !x.Name.ToString().IsNullOrEmpty())
                             .DistinctBy(x => x.TerritoryType.Row)
                             .OrderBy(x => x.ContentType.Row)
                             .ThenBy(x => x.ClassJobLevelRequired)
                             .ToDictionary(x => x.TerritoryType.Row, x => x));

    private static readonly Lazy<Dictionary<uint, Item>> gears =
        new(() => LuminaCache.Get<Item>()
                             .Where(x => x.EquipSlotCategory.Value.RowId != 0)
                             .DistinctBy(x => x.RowId)
                             .ToDictionary(x => x.RowId, x => x));

    private static readonly Lazy<Dictionary<uint, Item>> dyes =
        new(() => LuminaCache.Get<StainTransient>()
                             .Where(x => x.Item1.Value != null)
                             .ToDictionary(x => x.RowId, x => x.Item1.Value)!);

    private static readonly Lazy<Dictionary<uint, World>> cnWorlds =
        new(() => LuminaCache.Get<World>()
                             .Where(x => x.DataCenter.Value.Region == 5 &&
                                         !string.IsNullOrWhiteSpace(x.Name.RawString) &&
                                         !string.IsNullOrWhiteSpace(x.InternalName.RawString) &&
                                         IsChineseString(x.Name.RawString))
                             .ToDictionary(x => x.RowId, x => x));

    private static readonly Lazy<Dictionary<uint, TerritoryType>> zones =
        new(() => LuminaCache.Get<TerritoryType>()
                             .Where(x => !(string.IsNullOrWhiteSpace(x.Name.RawString) ||
                                           x.PlaceNameIcon <= 0 || x.PlaceNameRegionIcon <= 0))
                             .ToDictionary(x => x.RowId, x => x));

    private static readonly Lazy<Dictionary<uint, Mount>> mounts =
        new(() => LuminaCache.Get<Mount>()
                             .Where(x => !string.IsNullOrWhiteSpace(x.Singular.RawString) && x.Icon > 0)
                             .ToDictionary(x => x.RowId, x => x));

    private static readonly Lazy<Dictionary<uint, Item>> food =
        new(() => LuminaCache.Get<Item>()
                             .Where(x => !string.IsNullOrWhiteSpace(x.Name.RawString) && x.FilterGroup == 5)
                             .ToDictionary(x => x.RowId, x => x));

    #endregion
}
