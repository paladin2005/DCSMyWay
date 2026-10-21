using DCSMyWay.Models;

namespace DCSMyWay.Aircraft;

public static class MirageF1
{
  private const int Main = 1;
  private const int ParametersSelector = 3690;
  private const double PositionParameter = 0;
  private const double PresentPositionParameter = 0.2;
  private const int WaypointsWheel = 3694;
  private const double WheelStep = 0.111;
  private const int FirstDigitKey = 3697;
  private const int NorthKey = 3699;
  private const int SouthKey = 3705;
  private const int EastKey = 3703;
  private const int WestKey = 3701;
  private const int ClearKey = 3707;
  private const int InsertKey = 3711;
  private const int ParametersArgument = 665;
  private const int WheelArgument = 667;
  private const int InsertLightArgument = 1274;
  private const int NorthArgument = 1335;
  private const int SouthArgument = 1336;
  private const int EastArgument = 1359;
  private const int WestArgument = 1360;
  private const int LatitudeDigitsArgument = 1342;
  private const int LongitudeDigitsArgument = 1347;
  private const int Delay = 100;
  private const int SelectorDelay = 300;
  private const int DisplayTimeout = 1000;
  private const int WheelTimeout = 700;
  private const int LastWaypoint = 9;

  public static readonly IReadOnlyList<string> ModuleNames = ["Mirage-F1EE"];

  public static readonly SupportedAircraft Aircraft = new(
    "Mirage F1EE",
    ModuleNames,
    "waypoint",
    "WPT",
    1,
    LastWaypoint,
    "Turn on the INS so DCSMyWay can read the destination waypoint (VERS) on the PCN",
    (waypoints, start) => CreateTransfer(waypoints.Select(Format), start.Steerpoint));

  public static CockpitWaypoint Format(Waypoint waypoint) => new(
    waypoint.Name,
    CoordinateFormat.LatitudeHemisphere(waypoint.Latitude),
    CoordinateFormat.ToDegreesMinutes(waypoint.Latitude, 2, 1),
    CoordinateFormat.LongitudeHemisphere(waypoint.Longitude),
    CoordinateFormat.ToDegreesMinutes(waypoint.Longitude, 3, 1),
    CoordinateFormat.ToFeet(waypoint.ElevationMeters));

  public static TransferSequence CreateTransfer(IEnumerable<CockpitWaypoint> waypoints, int startWaypoint)
  {
    var steps = new List<CockpitStep>
    {
      new(Selector(PositionParameter))
      {
        WaitArguments = [Levels([ParametersArgument], [PositionParameter])],
        Timeout = DisplayTimeout,
        Failure = "The PCN parameters selector did not turn to POS",
      },
      new(Key(ClearKey))
      {
        UnlessArguments = [Levels([InsertLightArgument], [0])],
        WaitArguments = [Levels([InsertLightArgument], [0])],
        Timeout = DisplayTimeout,
        Failure = "The PCN kept an entry waiting for INSER after CLR",
      },
    };
    var waypointEnteredAfter = new List<int>();
    var number = startWaypoint;
    foreach (var waypoint in waypoints)
    {
      EnterWaypoint(steps, waypoint, number++);
      waypointEnteredAfter.Add(steps.Count);
    }

    steps.Add(new(Selector(PresentPositionParameter)));
    return new TransferSequence(steps, waypointEnteredAfter, [.. Enumerable.Range(startWaypoint, waypointEnteredAfter.Count).Select(n => (int?)n)]);
  }

  private static void EnterWaypoint(List<CockpitStep> steps, CockpitWaypoint waypoint, int number)
  {
    var name = waypoint.Name;
    steps.Add(new(new ButtonPress(Main, WaypointsWheel, Delay, WheelStep, false))
    {
      UntilArguments = [Levels([WheelArgument], [Math.Round((number - 1) * WheelStep, 3)])],
      Tries = LastWaypoint,
      Timeout = WheelTimeout,
      Failure = $"The PCN did not select waypoint {number}",
    });
    var north = waypoint.LatitudeHemisphere == 'N';
    var east = waypoint.LongitudeHemisphere == 'E';
    var latitude = Digits(LatitudeDigitsArgument, waypoint.Latitude);
    var longitude = Digits(LongitudeDigitsArgument, waypoint.Longitude);
    var latitudeHemisphere = north ? NorthArgument : SouthArgument;
    var longitudeHemisphere = east ? EastArgument : WestArgument;
    TypeCoordinate(steps, north ? NorthKey : SouthKey, latitudeHemisphere, latitude, Inserted([latitudeHemisphere], latitude), $"latitude of {name}");
    TypeCoordinate(steps, east ? EastKey : WestKey, longitudeHemisphere, longitude, Inserted([latitudeHemisphere, longitudeHemisphere], latitude, longitude), $"longitude of {name}");
  }

  private static void TypeCoordinate(List<CockpitStep> steps, int hemisphereKey, int hemisphereArgument, ArgumentCheck digits, ArgumentCheck inserted, string what)
  {
    steps.Add(new(Key(hemisphereKey))
    {
      WaitArguments = [Levels([InsertLightArgument, hemisphereArgument], [1, 1])],
      Timeout = DisplayTimeout,
      Failure = $"The PCN did not start the {what}",
    });
    var keys = digits.Levels.Select(level => FirstDigitKey + (int)Math.Round(level * 10)).ToList();
    foreach (var key in keys[..^1]) steps.Add(new(Key(key)));
    steps.Add(new(Key(keys[^1])) { WaitArguments = [digits], Timeout = DisplayTimeout, Failure = $"The PCN does not show the {what} as typed" });
    steps.Add(new(Key(InsertKey)) { WaitArguments = [inserted], Timeout = DisplayTimeout, Failure = $"The PCN did not insert the {what}" });
  }

  private static ArgumentCheck Digits(int firstArgument, string coordinate)
  {
    var digits = coordinate.Replace(".", "");
    return Levels([.. Enumerable.Range(firstArgument, digits.Length)], [.. digits.Select(d => (d - '0') / 10.0)]);
  }

  private static ArgumentCheck Inserted(IReadOnlyList<int> hemispheres, params ArgumentCheck[] digits) => Levels(
    [InsertLightArgument, .. hemispheres, .. digits.SelectMany(d => d.Arguments)],
    [0, .. hemispheres.Select(_ => 1.0), .. digits.SelectMany(d => d.Levels)]);

  private static ArgumentCheck Levels(IReadOnlyList<int> arguments, IReadOnlyList<double> levels) => new(arguments, levels);

  private static ButtonPress Key(int code) => new(Main, code, Delay);

  private static ButtonPress Selector(double position) => new(Main, ParametersSelector, SelectorDelay, position, false);
}
