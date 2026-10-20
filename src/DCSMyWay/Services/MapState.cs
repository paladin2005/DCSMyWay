using System.Text.Json;
using System.Text.Json.Serialization;
using DCSMyWay.Models;

namespace DCSMyWay.Services;

public sealed record MapWaypoint(string Name, double Lat, double Lon, int? Steerpoint);

public sealed record MapPosition(double Lat, double Lon);

public sealed record MapState(List<MapWaypoint> Waypoints, MapPosition? Camera)
{
  public static MapState Create(IEnumerable<Waypoint> waypoints, Func<Waypoint, int?> steerpointOf, DcsPosition? camera) => new(
    [.. waypoints.Select(w => new MapWaypoint(w.Name, w.Latitude, w.Longitude, steerpointOf(w)))],
    camera is null ? null : new MapPosition(camera.Latitude, camera.Longitude));

  public string ToJson() => JsonSerializer.Serialize(this, MapJsonContext.Default.MapState);
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(MapState))]
internal sealed partial class MapJsonContext : JsonSerializerContext;
