using DCSMyWay.Models;

namespace DCSMyWay.Aircraft;

public static class F16
{
  private const int Icp = 17;
  private const int ReturnSwitch = 3032;
  private const int SteerpointIncrement = 3030;
  private const int DobberUp = 3034;
  private const int DobberDown = 3035;
  private const int Enter = 3016;
  private const int ShortDelay = 10;
  private const int LongDelay = 150;

  private static readonly Dictionary<char, int> DigitCodes = new()
  {
    ['0'] = 3002,
    ['1'] = 3003,
    ['2'] = 3004,
    ['3'] = 3005,
    ['4'] = 3006,
    ['5'] = 3007,
    ['6'] = 3008,
    ['7'] = 3009,
    ['8'] = 3010,
    ['9'] = 3011,
  };

  public static readonly IReadOnlyList<string> ModuleNames =
  [
    "F-16C_50", "F-16D_50", "F-16D_50_NS", "F-16D_52", "F-16D_52_NS", "F-16D_Barak_30", "F-16D_Barak_40", "F-16I",
  ];

  public static CockpitWaypoint Format(Waypoint waypoint) => new(
    waypoint.Name,
    CoordinateFormat.LatitudeHemisphere(waypoint.Latitude),
    CoordinateFormat.ToDegreesMinutes(waypoint.Latitude, 2, 3),
    CoordinateFormat.LongitudeHemisphere(waypoint.Longitude),
    CoordinateFormat.ToDegreesMinutes(waypoint.Longitude, 3, 3),
    CoordinateFormat.ToFeet(waypoint.ElevationMeters));

  public static List<ButtonPress> CreateButtonPresses(IEnumerable<CockpitWaypoint> waypoints, bool startAtActiveSteerpoint = true, int extraDelay = 0)
  {
    var shortDelay = ShortDelay + extraDelay;
    var longDelay = LongDelay + extraDelay;
    var presses = new List<ButtonPress>
    {
      new(Icp, ReturnSwitch, longDelay, -1),
      new(Icp, DigitCodes['4'], longDelay),
    };

    void TypeDigits(string text)
    {
      foreach (var character in text)
      {
        if (DigitCodes.TryGetValue(character, out var code)) presses.Add(new(Icp, code, shortDelay));
      }
    }

    var isFirst = true;
    foreach (var waypoint in waypoints)
    {
      if (!isFirst || !startAtActiveSteerpoint) presses.Add(new(Icp, SteerpointIncrement, longDelay));
      isFirst = false;
      presses.Add(new(Icp, DobberDown, longDelay, -1));
      presses.Add(new(Icp, DobberDown, longDelay, -1));
      TypeDigits(waypoint.LatitudeHemisphere == 'N' ? "2" : "8");
      TypeDigits(waypoint.Latitude);
      presses.Add(new(Icp, Enter, shortDelay));
      presses.Add(new(Icp, DobberDown, longDelay, -1));
      TypeDigits(waypoint.LongitudeHemisphere == 'E' ? "6" : "4");
      TypeDigits(waypoint.Longitude);
      presses.Add(new(Icp, Enter, shortDelay));
      presses.Add(new(Icp, DobberDown, longDelay, -1));
      TypeDigits(waypoint.ElevationFeet);
      presses.Add(new(Icp, Enter, shortDelay));
      for (var i = 0; i < 4; i++) presses.Add(new(Icp, DobberUp, longDelay));
    }

    presses.Add(new(Icp, ReturnSwitch, longDelay, -1));
    return presses;
  }
}
