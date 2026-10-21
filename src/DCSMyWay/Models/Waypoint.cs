using System.Text.RegularExpressions;

namespace DCSMyWay.Models;

public sealed partial record Waypoint(int Id, string Name, double Latitude, double Longitude, double ElevationMeters, bool IsTarget = false, string? Threat = null)
{
  public const int MaxNameLength = 12;

  public static bool IsValidName(string name) => ValidName().IsMatch(name);

  [GeneratedRegex("^[A-Z][A-Z0-9.]{0,11}$")]
  private static partial Regex ValidName();
}
