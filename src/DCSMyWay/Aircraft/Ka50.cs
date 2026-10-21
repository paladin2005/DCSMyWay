using System.Globalization;
using DCSMyWay.Models;

namespace DCSMyWay.Aircraft;

public static class Ka50
{
  private const int Pvi = 20;
  private const int PviIndicator = 5;
  private const int ModeSwitch = 3026;
  private const double EntryMode = 0.2;
  private const double OperationMode = 0.3;
  private const int WaypointButton = 3011;
  private const int TargetButton = 3017;
  private const int EnterButton = 3018;
  private const int CancelButton = 3019;
  private const int PositiveKey = 3001;
  private const int NegativeKey = 3002;
  private const int Delay = 100;
  private const int SwitchDelay = 300;
  private const int DisplayTimeout = 1000;
  private const int ReadbackTimeout = 2000;
  private const int LastWaypoint = 6;
  private const int LastTargetPoint = 9;
  private const string LatitudeElement = "^txt_VIT$";
  private const string LongitudeElement = "^txt_NIT$";
  private const string LatitudeSignElement = "^txt_VIT_sign$";
  private const string LongitudeSignElement = "^txt_NIT_sign$";

  private static readonly PviMode WaypointMode = new(WaypointButton, "^txt_OIT_PPM$", "waypoint");
  private static readonly PviMode TargetMode = new(TargetButton, "^txt_OIT_NOT$", "target");

  public static readonly IReadOnlyList<string> ModuleNames = ["Ka-50", "Ka-50_3"];

  public static readonly SupportedAircraft Aircraft = new(
    "Ka-50",
    ModuleNames,
    "waypoint",
    "WPT",
    1,
    LastWaypoint,
    "Set the PVI-800 to OPER and select a waypoint with WPT so DCSMyWay can read the active waypoint",
    (waypoints, start) => CreateTransfer(waypoints, start.Steerpoint, start.Target))
  {
    SupportsTargets = true,
    LastTarget = LastTargetPoint,
  };

  public static CockpitWaypoint Format(Waypoint waypoint) => new(
    waypoint.Name,
    CoordinateFormat.LatitudeHemisphere(waypoint.Latitude),
    CoordinateFormat.ToDegreesMinutes(waypoint.Latitude, 2, 1),
    CoordinateFormat.LongitudeHemisphere(waypoint.Longitude),
    CoordinateFormat.ToDegreesMinutes(waypoint.Longitude, 3, 1),
    CoordinateFormat.ToFeet(waypoint.ElevationMeters));

  public static TransferSequence CreateTransfer(IEnumerable<Waypoint> waypoints, int startWaypoint, int firstTarget)
  {
    var steps = new List<CockpitStep> { new(Switch(OperationMode)), new(Switch(EntryMode)) };
    var waypointEnteredAfter = new List<int>();
    var steerpoints = new List<int?>();
    var waypointNumber = startWaypoint;
    var targetNumber = firstTarget;
    PviMode? lit = null;
    foreach (var waypoint in waypoints)
    {
      var mode = waypoint.IsTarget ? TargetMode : WaypointMode;
      var number = waypoint.IsTarget ? targetNumber++ : waypointNumber++;
      EnterPoint(steps, mode, lit, Format(waypoint), number);
      lit = mode;
      steerpoints.Add(waypoint.IsTarget ? null : number);
      waypointEnteredAfter.Add(steps.Count);
    }

    steps.Add(new(Key(CancelButton)));
    steps.Add(new(Switch(OperationMode)));
    return new TransferSequence(steps, waypointEnteredAfter, steerpoints);
  }

  private static void EnterPoint(List<CockpitStep> steps, PviMode mode, PviMode? lit, CockpitWaypoint waypoint, int number)
  {
    var name = waypoint.Name;
    var southern = waypoint.LatitudeHemisphere == 'S';
    var western = waypoint.LongitudeHemisphere == 'W';
    OpenMode(steps, mode, lit);
    steps.Add(new(NumberKey(number))
    {
      Wait = [new(PviIndicator, mode.NumberElement, $"^%s*{number}$")],
      Timeout = DisplayTimeout,
      Failure = $"The PVI-800 did not select {mode.Name} {number}",
    });
    TypeCoordinate(steps, southern, waypoint.Latitude, LatitudeElement, LatitudeSignElement, $"The PVI-800 does not show the latitude typed for {name}");
    TypeCoordinate(steps, western, waypoint.Longitude, LongitudeElement, LongitudeSignElement, $"The PVI-800 does not show the longitude typed for {name}");
    steps.Add(new(Key(EnterButton)));
    OpenMode(steps, mode, mode);
    var latitudeFailure = $"The PVI-800 did not store the latitude of {name}";
    var longitudeFailure = $"The PVI-800 did not store the longitude of {name}";
    steps.Add(new(NumberKey(number)) { Wait = [Stored(LatitudeElement, waypoint.Latitude)], Timeout = ReadbackTimeout, Failure = latitudeFailure });
    steps.Add(new(null) { Wait = [Stored(LongitudeElement, waypoint.Longitude)], Timeout = ReadbackTimeout, Failure = longitudeFailure });
    if (southern) steps.Add(SignCheck(LatitudeSignElement, latitudeFailure));
    if (western) steps.Add(SignCheck(LongitudeSignElement, longitudeFailure));
  }

  private static void OpenMode(List<CockpitStep> steps, PviMode mode, PviMode? lit)
  {
    if (lit == mode) steps.Add(new(Key(mode.Button)));
    steps.Add(mode == WaypointMode
      ? new(Key(mode.Button)) { Wait = [new(PviIndicator, mode.NumberElement)], Timeout = DisplayTimeout, Failure = "The PVI-800 did not list its waypoints. Is it on?" }
      : new(Key(mode.Button)));
  }

  private static void TypeCoordinate(List<CockpitStep> steps, bool negative, string coordinate, string element, string signElement, string failure)
  {
    var digits = coordinate.Replace(".", "");
    steps.Add(new(Key(negative ? NegativeKey : PositiveKey)));
    foreach (var digit in digits[..^1]) steps.Add(new(DigitKey(digit)));
    steps.Add(new(DigitKey(digits[^1])) { Wait = [new(PviIndicator, element, $"^%s*{digits}$")], Timeout = DisplayTimeout, Failure = failure });
    if (negative) steps.Add(SignCheck(signElement, failure));
  }

  private static CockpitStep SignCheck(string element, string failure) =>
    new(null) { Wait = [new(PviIndicator, element, Values: ["-"])], Timeout = DisplayTimeout, Failure = failure };

  private static IndicationCheck Stored(string element, string coordinate)
  {
    var parts = coordinate.Split('.');
    var degreeFormat = new string('0', parts[0].Length);
    var tenths = int.Parse(parts[0], CultureInfo.InvariantCulture) * 600
      + int.Parse(parts[1], CultureInfo.InvariantCulture) * 10
      + int.Parse(parts[2], CultureInfo.InvariantCulture);
    var readings = new[] { tenths, tenths - 1, tenths + 1 }
      .Where(v => v >= 0)
      .Select(v => string.Create(CultureInfo.InvariantCulture, $"{(v / 600).ToString(degreeFormat, CultureInfo.InvariantCulture)}{v % 600 / 10:00}{v % 10}"));
    return new(PviIndicator, element, Values: [.. readings.SelectMany(r => new[] { r, $" {r}" })]);
  }

  private static ButtonPress NumberKey(int number) => DigitKey((char)('0' + number % 10));

  private static ButtonPress DigitKey(char digit) => Key(PositiveKey + digit - '0');

  private static ButtonPress Key(int code) => new(Pvi, code, Delay);

  private static ButtonPress Switch(double position) => new(Pvi, ModeSwitch, SwitchDelay, position, false);

  private sealed record PviMode(int Button, string NumberElement, string Name);
}
