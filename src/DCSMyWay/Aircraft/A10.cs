using System.Globalization;
using DCSMyWay.Models;

namespace DCSMyWay.Aircraft;

public static class A10
{
  private const int Cdu = 9;
  private const int Aap = 22;
  private const int CduIndicator = 3;
  private const int SelectWaypointKey = 3001;
  private const int ElevationKey = 3002;
  private const int LatitudeKey = 3003;
  private const int LongitudeKey = 3004;
  private const int NameKey = 3005;
  private const int WaypointPageKey = 3005;
  private const int CopyToNewWaypointKey = 3007;
  private const int WaypointMenuKey = 3011;
  private const int FlightPlanMenuKey = 3013;
  private const int PageUpKey = 3062;
  private const int PageDownKey = 3063;
  private const int ClearKey = 3058;
  private const int PageSelector = 3004;
  private const double OtherPage = 0;
  private const int Delay = 100;
  private const int PageTimeout = 1000;
  private const int ReadbackTimeout = 2000;
  private const int PageTurnTimeout = 300;
  private const int FlightPlanListPages = 7;
  private const int FlightPlanPages = 14;
  private const int LastWaypoint = 50;
  private const string InputError = "CDU INPUT ERR";

  public static readonly IReadOnlyList<string> ModuleNames = ["A-10C", "A-10C_2"];

  public static readonly SupportedAircraft Aircraft = new(
    "A-10C",
    ModuleNames,
    "steerpoint",
    "STPT",
    1,
    LastWaypoint,
    "Turn on the CDU so DCSMyWay can read the active steerpoint",
    (waypoints, start, _) => CreateTransfer(waypoints.Select(Format), start));

  public static CockpitWaypoint Format(Waypoint waypoint) => new(
    waypoint.Name,
    CoordinateFormat.LatitudeHemisphere(waypoint.Latitude),
    CoordinateFormat.ToDegreesMinutes(waypoint.Latitude, 2, 3),
    CoordinateFormat.LongitudeHemisphere(waypoint.Longitude),
    CoordinateFormat.ToDegreesMinutes(waypoint.Longitude, 3, 3),
    CoordinateFormat.ToFeet(waypoint.ElevationMeters));

  public static TransferSequence CreateTransfer(IEnumerable<CockpitWaypoint> waypoints, int startWaypoint)
  {
    var steps = new List<CockpitStep>
    {
      new(new ButtonPress(Aap, PageSelector, Delay, OtherPage, false)),
      new(Key(ClearKey)),
      PageStep(WaypointMenuKey, "^PageNameWPMENU$", "The CDU did not open WP MENU. Is it on?"),
      PageStep(WaypointPageKey, "^PageNameWAYPT$", "The CDU did not open the WAYPT page"),
    };
    var waypointEnteredAfter = new List<int>();
    var entered = waypoints.Select((waypoint, index) => (waypoint, startWaypoint + index)).ToList();
    foreach (var (waypoint, number) in entered)
    {
      EnterWaypoint(steps, waypoint, number);
      waypointEnteredAfter.Add(steps.Count);
    }

    AddToFlightPlan(steps, entered);
    return new TransferSequence(steps, waypointEnteredAfter, [.. Enumerable.Range(startWaypoint, waypointEnteredAfter.Count).Select(n => (int?)n)]);
  }

  private static void AddToFlightPlan(List<CockpitStep> steps, List<(CockpitWaypoint Waypoint, int Number)> waypoints)
  {
    steps.Add(PageStep(FlightPlanMenuKey, "^PageNameFPMENU$", "The CDU did not open FPMENU"));
    steps.Add(new(null)
    {
      Options =
      [
        new(Check("^ActiveFP1$", "*"), Key(3005)),
        new(Check("^ActiveFP3$", "*"), Key(3006)),
        new(Check("^ActiveFP5$", "*"), Key(3007)),
      ],
      Otherwise = PageDown,
      Tries = FlightPlanListPages,
      Timeout = PageTurnTimeout,
      Failure = "There is no active flight plan",
    });
    steps.Add(new(null) { Wait = [new(CduIndicator, "^PageNameFPBUILD$")], Timeout = PageTimeout, Failure = "The CDU did not open FPBUILD" });
    foreach (var (waypoint, number) in waypoints)
    {
      var target = number.ToString(CultureInfo.InvariantCulture);
      steps.Add(new(null)
      {
        Options = [new(new(CduIndicator, "^FPBUILDPageNumber$", "^1/"))],
        Otherwise = PageUp,
        Tries = FlightPlanPages,
        Timeout = PageTurnTimeout,
        Failure = "The CDU did not go back to the first page of the flight plan",
      });
      Type(steps, target);
      steps.Add(new(null)
      {
        Options =
        [
          new(Check("^WPNumber%d*$", target), Key(ClearKey)),
          new(new(CduIndicator, "^WPNewName$", "NEXT"), Key(3002)),
          new(new(CduIndicator, "^WPNewName2$", "NEXT"), Key(3003)),
          new(new(CduIndicator, "^WPNewName4$", "NEXT"), Key(3004)),
        ],
        Otherwise = PageDown,
        Tries = FlightPlanPages,
        Timeout = PageTurnTimeout,
        Failure = $"Could not add {waypoint.Name} to the active flight plan. Is it full?",
      });
      steps.Add(new(null)
      {
        Wait = [Check("^WPName%d*$", waypoint.Name)],
        Timeout = PageTimeout,
        Failure = $"{waypoint.Name} did not appear in the active flight plan",
      });
    }
  }

  private static void EnterWaypoint(List<CockpitStep> steps, CockpitWaypoint waypoint, int number)
  {
    var name = waypoint.Name;
    var target = number.ToString(CultureInfo.InvariantCulture);
    Type(steps, target);
    steps.Add(new(Key(SelectWaypointKey))
    {
      Wait = [Check("^WAYPTNumber1$", target), Check("^ScratchString$", InputError)],
      Timeout = PageTimeout,
      Failure = $"The CDU did not select waypoint {number}",
    });
    steps.Add(new(Key(ClearKey)));
    steps.Add(new(Key(CopyToNewWaypointKey))
    {
      Until = [Check("^WAYPTNumber1$", target)],
      Timeout = PageTimeout,
      Failure = $"Waypoint {number} does not exist and could not be created",
    });
    Type(steps, waypoint.LatitudeHemisphere + waypoint.Latitude.Replace(".", ""));
    steps.Add(Readback(
      LatitudeKey,
      "^WAYPTLat$",
      CoordinateReadings(waypoint.LatitudeHemisphere, waypoint.Latitude, 2),
      $"The CDU does not show the latitude entered for {name}"));
    Type(steps, waypoint.LongitudeHemisphere + waypoint.Longitude.Replace(".", ""));
    steps.Add(Readback(
      LongitudeKey,
      "^WAYPTLong$",
      CoordinateReadings(waypoint.LongitudeHemisphere, waypoint.Longitude, 3),
      $"The CDU does not show the longitude entered for {name}"));
    Type(steps, waypoint.ElevationFeet);
    steps.Add(Readback(ElevationKey, "^WAYPT_EL3$", ElevationReadings(waypoint.ElevationFeet), $"The CDU does not show the elevation entered for {name}"));
    Type(steps, name);
    steps.Add(Readback(NameKey, "^WAYPTIdent1$", [name], $"The CDU did not take the name {name}"));
    steps.Add(new(null)
    {
      Wait = [Check("^WAYPTNumber1$", target)],
      Timeout = PageTimeout,
      Failure = $"Another waypoint is already named {name}",
    });
  }

  private static CockpitStep PageStep(int key, string page, string failure) =>
    new(Key(key)) { Wait = [new(CduIndicator, page)], Timeout = PageTimeout, Failure = failure };

  private static CockpitStep Readback(int key, string element, IReadOnlyList<string> values, string failure) =>
    new(Key(key)) { Wait = [new(CduIndicator, element, Values: values)], Timeout = ReadbackTimeout, Failure = failure };

  private static IndicationCheck Check(string element, string value) => new(CduIndicator, element, Values: [value]);

  private static IReadOnlyList<string> CoordinateReadings(char hemisphere, string coordinate, int degreeDigits)
  {
    var parts = coordinate.Split('.');
    var thousandths = int.Parse(parts[0], CultureInfo.InvariantCulture) * 60_000
      + int.Parse(parts[1], CultureInfo.InvariantCulture) * 1_000
      + int.Parse(parts[2], CultureInfo.InvariantCulture);
    var degreeFormat = new string('0', degreeDigits);
    return [.. Neighbors(thousandths).Select(v => string.Create(
      CultureInfo.InvariantCulture,
      $"{hemisphere}{(v / 60_000).ToString(degreeFormat, CultureInfo.InvariantCulture)}°{v % 60_000 / 1_000:00}.{v % 1_000:000}"))];
  }

  private static IReadOnlyList<string> ElevationReadings(string feet)
  {
    var value = int.Parse(new string([.. feet.Where(char.IsAsciiDigit)]), CultureInfo.InvariantCulture);
    return [.. Neighbors(value).Select(v => v.ToString(CultureInfo.InvariantCulture))];
  }

  private static int[] Neighbors(int value) => [value, value - 1, value + 1];

  private static void Type(List<CockpitStep> steps, string text)
  {
    foreach (var code in text.Select(KeyCode).OfType<int>()) steps.Add(new(Key(code)));
  }

  private static int? KeyCode(char character) => character switch
  {
    >= '1' and <= '9' => 3015 + character - '1',
    '0' => 3024,
    '.' => 3025,
    >= 'A' and <= 'Z' => 3027 + character - 'A',
    >= 'a' and <= 'z' => 3027 + character - 'a',
    _ => null,
  };

  private static ButtonPress Key(int code) => new(Cdu, code, Delay);

  private static ButtonPress PageUp => Key(PageUpKey);

  private static ButtonPress PageDown => new(Cdu, PageDownKey, Delay, -1);
}
