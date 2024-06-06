using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Utility;
using Lumina.Excel.GeneratedSheets;

namespace DailyRoutines.Helpers;

public static class MapHelper
{
    // 材质坐标, 地图坐标 和 世界坐标
    #region Position
    #region Texture & Map
    public static Vector2 TextureToMap(Vector2 textureCoordinates, Map map)
    {
        return WorldToMap(TextureToWorld(textureCoordinates, map), map);
    }

    public static Vector2 MapToTexture(Vector2 mapCoordinates, Map map)
    {
        return WorldToTexture(MapToWorld(mapCoordinates, map).ToVector3(), map);
    }
    #endregion

    #region World & Map
    public static Vector2 WorldToMap(Vector2 worldCoordinates, Map map)
    {
        return new Vector2(
            WorldXZToMap(worldCoordinates.X, map.SizeFactor, map.OffsetX),
            WorldXZToMap(worldCoordinates.Y, map.SizeFactor, map.OffsetY));
    }

    public static Vector3 WorldToMap(Vector3 worldCoordinates, Map map, TerritoryTypeTransient territoryTransient, bool correctZOffset = false)
    {
        return new Vector3(
            WorldXZToMap(worldCoordinates.X, map.SizeFactor, map.OffsetX),
            WorldXZToMap(worldCoordinates.Z, map.SizeFactor, map.OffsetY),
            WorldYToMap(worldCoordinates.Y, territoryTransient.OffsetZ, correctZOffset));
    }

    public static Vector2 MapToWorld(Vector2 mapCoordinates, Map map)
    {
        return new Vector2(
            MapToWorldXZ(mapCoordinates.X, map.SizeFactor, map.OffsetX),
            MapToWorldXZ(mapCoordinates.Y, map.SizeFactor, map.OffsetY));
    }

    public static Vector3 MapToWorld(Vector3 mapCoordinates, Map map, TerritoryTypeTransient territoryTransient, bool correctZOffset = false)
    {
        return new Vector3(
            MapToWorldXZ(mapCoordinates.X, map.SizeFactor, map.OffsetX),
            MapToWorldXZ(mapCoordinates.Z, map.SizeFactor, map.OffsetY),
            MapToWorldY(mapCoordinates.Y, territoryTransient.OffsetZ, correctZOffset));
    }
    #endregion

    #region World & Texture
    public static Vector2 WorldToTexture(Vector3 position, Map map)
    {
        return new Vector2(position.X, position.Z) * (map.SizeFactor / 100.0f) +
           new Vector2(map.OffsetX, map.OffsetY) * (map.SizeFactor / 100.0f) +
           new Vector2(1024.0f, 1024.0f);
    }

    public static Vector2 TextureToWorld(Vector2 coordinates, Map map)
    {
        var adjustedCoordinates = (coordinates - new Vector2(1024f)) / map.SizeFactor;
        return adjustedCoordinates * 100f - new Vector2(map.OffsetX, map.OffsetY);
    }
    #endregion

    #region Helper Methods
    private static float WorldXZToMap(float value, uint scale, int offset)
    {
        return 0.02f * offset + 2048f / scale + 0.02f * value + 1f;
    }

    private static float MapToWorldXZ(float mapValue, uint scale, int offset)
    {
        return (mapValue - 0.02f * offset - 2048f / scale - 1f) / 0.02f;
    }

    public static float WorldYToMap(float value, int zOffset, bool correctZOffset = false)
    {
        return (correctZOffset && zOffset == -10000) ? value / 100f : (value - zOffset) / 100f;
    }

    public static float MapToWorldY(float mapValue, int zOffset, bool correctZOffset = false)
    {
        return (correctZOffset && zOffset == -10000) ? mapValue * 100f : mapValue * 100f + zOffset;
    }
    #endregion
    #endregion

    #region MapMarker Extensions
    private static string GetMarkerPlaceName(this MapMarker marker)
    {
        var placeName = marker.GetMarkerLabel();
        if (placeName != string.Empty) return placeName;

        var mapSymbol = LuminaCache.GetRow<MapSymbol>(marker.Icon);
        return mapSymbol?.PlaceName.Value?.Name.ToDalamudString().TextValue ?? string.Empty;
    }

    public static string GetMarkerLabel(this MapMarker marker)
        => LuminaCache.GetRow<PlaceName>(marker.PlaceNameSubtext.Row)!.Name.ToDalamudString().TextValue;

    public static Vector2 GetPosition(this MapMarker marker) => new(marker.X, marker.Y);
    #endregion

    public static List<MapMarker> GetMapMarkers(uint mapID)
        => GetMapMarkers(LuminaCache.GetRow<Map>(mapID));

    public static List<MapMarker> GetMapMarkers(this Map map)
    {
        return LuminaCache.Get<MapMarker>()
                          .Where(x => x.RowId == map.MapMarkerRange)
                          .ToList();
    }
}
