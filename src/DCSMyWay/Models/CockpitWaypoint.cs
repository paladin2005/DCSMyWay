namespace DCSMyWay.Models;

public sealed record CockpitWaypoint(
  string Name,
  char LatitudeHemisphere,
  string Latitude,
  char LongitudeHemisphere,
  string Longitude,
  string ElevationFeet);
