using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Utility;
using Lumina.Excel.GeneratedSheets;

namespace DailyRoutines.Helpers;

public static class MapHelper
{
    // 材质坐标, 地图坐标 和 世界坐标
    #region Position
    public static Vector2 TextureToMap(Vector2 textureCoordinates, Map map)
    {
        var worldPosition = TextureToWorld(textureCoordinates, map);
        return WorldToMap(worldPosition, map);
    }

    public static Vector2 MapToTexture(Vector2 mapCoordinates, Map map)
    {
        var worldPosition = MapToWorld(mapCoordinates, map);
        return WorldToTexture(worldPosition, map);
    }

    public static Vector2 WorldToMap(Vector2 worldCoordinates, Map map)
    {
        var scalar = map.SizeFactor / 100.0f;
        var center = new Vector2(1024.0f, 1024.0f);

        var adjustedWorldCoordinates = worldCoordinates + center / scalar;

        var scaledWorldCoordinates = adjustedWorldCoordinates * scalar;

        var mapX = MapToWorld(scaledWorldCoordinates.X, map.SizeFactor, map.OffsetX);
        var mapY = MapToWorld(scaledWorldCoordinates.Y, map.SizeFactor, map.OffsetY);

        return new Vector2(mapX, mapY);
    }

    public static Vector2 MapToWorld(Vector2 coordinates, Map map)
    {
        var scalar = map.SizeFactor / 100.0f;

        var xWorldCoord = MapToWorld(coordinates.X, map.SizeFactor, map.OffsetX);
        var yWorldCoord = MapToWorld(coordinates.Y, map.SizeFactor, map.OffsetY);

        var objectPosition = new Vector2(xWorldCoord, yWorldCoord);
        var center = new Vector2(1024.0f, 1024.0f);

        return objectPosition / scalar - center / scalar;
    }

    public static float MapToWorld(float value, uint scale, int offset)
        => -offset * (scale / 100.0f) + 50.0f * (value - 1) * (scale / 100.0f);

    public static Vector2 WorldToTexture(GameObject gameObject, Map map)
        => WorldToTexture(gameObject.Position, map);

    public static Vector2 WorldToTexture(Vector3 position, Map map)
        => WorldToTexture(new Vector2(position.X, position.Z), map);

    public static Vector2 WorldToTexture(Vector2 coordinates, Map map)
        => coordinates * (map.SizeFactor / 100.0f) +
           new Vector2(map.OffsetX, map.OffsetY) * (map.SizeFactor / 100.0f) +
           new Vector2(1024.0f, 1024.0f);

    public static Vector2 TextureToWorld(Vector2 coordinates, Map map)
        => TextureToWorld(coordinates, new(map.OffsetX, map.OffsetY), map.SizeFactor);

    public static Vector2 TextureToWorld(Vector2 coordinates, Vector2 mapOffset, ushort mapSizeFactor)
    {
        var adjustedCoordinates = coordinates - new Vector2(1024f);

        adjustedCoordinates /= mapSizeFactor;

        return (adjustedCoordinates * 100f) - mapOffset;
    }
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
