using System.Globalization;
using DCSMyWay.Models;

namespace DCSMyWay.Aircraft;

public static class F18
{
  private const int Ufc = 25;
  private const int Ampcd = 37;
  private const int AmpcdIndicator = 4;
  private const int UfcIndicator = 6;
  private const int HsiButton = 3012;
  private const int UfcDataButton = 3015;
  private const int AircraftDataTab = 3016;
  private const int WaypointDataTab = 3017;
  private const int DataButton = 3020;
  private const int ReturnToHsiButton = 3020;
  private const int NextWaypoint = 3022;
  private const int PreviousWaypoint = 3023;
  private const int LatLongFormatButton = 3025;
  private const int MenuButton = 3028;
  private const int PreciseButton = 3029;
  private const int Option1 = 3010;
  private const int Option3 = 3012;
  private const int DigitZero = 3018;
  private const int Enter = 3029;
  private const int ShortDelay = 100;
  private const int OptionDelay = 200;
  private const int ElevationEnterDelay = 500;
  private const int UfcDataDelay = 800;
  private const int EnterDelay = 1000;
  private const int PageTimeout = 1000;
  private const int UfcTimeout = 1500;
  private const int ReadbackTimeout = 3000;
  private const int MenuPresses = 3;
  private const int LastWaypoint = 59;

  private static readonly IndicationCheck SuptMenu = new(AmpcdIndicator, "^Menu_SUPT$");
  private static readonly IndicationCheck HsiPage = new(AmpcdIndicator, "^HSELData_Label$");
  private static readonly IndicationCheck DataPage = new(AmpcdIndicator, "^MDATA_");
  private static readonly IndicationCheck AircraftDataPage = new(AmpcdIndicator, "^AC_Latitude$");
  private static readonly IndicationCheck DecimalMinutes = new(AmpcdIndicator, "^AC_Latitude$", "%.%d+'$");
  private static readonly IndicationCheck WaypointDataPage = new(AmpcdIndicator, "^WYPT_Number$");
  private static readonly IndicationCheck PreciseOn = new(AmpcdIndicator, "^PRECISE_%d+_box");

  public static readonly IReadOnlyList<string> ModuleNames = ["FA-18C_hornet"];

  public static readonly SupportedAircraft Aircraft = new(
    "F/A-18C",
    ModuleNames,
    "waypoint",
    "WYPT",
    1,
    LastWaypoint,
    "Show the HSI on a display so DCSMyWay can read the selected waypoint",
    (waypoints, start, _) => CreateTransfer(waypoints.Select(Format), start));

  public static CockpitWaypoint Format(Waypoint waypoint) => new(
    waypoint.Name,
    CoordinateFormat.LatitudeHemisphere(waypoint.Latitude),
    CoordinateFormat.ToDegreesMinutes(waypoint.Latitude, 2, 4),
    CoordinateFormat.LongitudeHemisphere(waypoint.Longitude),
    CoordinateFormat.ToDegreesMinutes(waypoint.Longitude, 3, 4),
    CoordinateFormat.ToFeet(waypoint.ElevationMeters));

  public static TransferSequence CreateTransfer(IEnumerable<CockpitWaypoint> waypoints, int startWaypoint)
  {
    var steps = new List<CockpitStep>();
    var waypointEnteredAfter = new List<int>();
    OpenWaypointData(steps);
    var number = startWaypoint;
    foreach (var waypoint in waypoints)
    {
      steps.Add(SelectWaypoint(number++));
      EnterWaypoint(steps, waypoint);
      waypointEnteredAfter.Add(steps.Count);
    }

    steps.Add(SelectWaypoint(startWaypoint));
    steps.Add(PageStep(ReturnToHsiButton, [HsiPage], "The AMPCD did not go back to the HSI"));
    return new TransferSequence(steps, waypointEnteredAfter);
  }

  private static void OpenWaypointData(List<CockpitStep> steps)
  {
    steps.Add(PageStep(MenuButton, [SuptMenu, HsiPage, DataPage], "The AMPCD did not show the SUPT menu", MenuPresses));
    steps.Add(PageStep(HsiButton, [HsiPage, DataPage], "The AMPCD did not open the HSI"));
    steps.Add(PageStep(DataButton, [DataPage], "The AMPCD did not open HSI > DATA"));
    steps.Add(PageStep(AircraftDataTab, [AircraftDataPage], "The AMPCD did not open DATA > A/C"));
    steps.Add(PageStep(LatLongFormatButton, [DecimalMinutes], "LATLN did not switch to decimal minutes"));
    steps.Add(PageStep(WaypointDataTab, [WaypointDataPage], "The AMPCD did not open DATA > WYPT"));
    steps.Add(PageStep(PreciseButton, [PreciseOn], "PRECISE did not turn on"));
  }

  private static void EnterWaypoint(List<CockpitStep> steps, CockpitWaypoint waypoint)
  {
    var name = waypoint.Name;
    steps.Add(UfcStep(AmpcdButton(UfcDataButton, UfcDataDelay), "^UFC_OptionDisplay1$", "POSN", $"The UFC did not open the data of {name}"));
    steps.Add(UfcStep(UfcButton(Option1, OptionDelay), "^UFC_OptionCueing1$", ":", $"The UFC did not select POSN for {name}"));
    steps.Add(UfcStep(
      Digit(waypoint.LatitudeHemisphere == 'N' ? '2' : '8'),
      "^UFC_ScratchPadString1Display$",
      waypoint.LatitudeHemisphere.ToString(),
      $"The UFC did not take the latitude of {name}"));
    TypeDigits(steps, waypoint.Latitude[..^5]);
    steps.Add(new(UfcButton(Enter, EnterDelay)));
    TypeDigits(steps, waypoint.Latitude[^5..]);
    steps.Add(new(UfcButton(Enter, EnterDelay)));
    steps.Add(UfcStep(
      Digit(waypoint.LongitudeHemisphere == 'E' ? '6' : '4'),
      "^UFC_ScratchPadString1Display$",
      waypoint.LongitudeHemisphere.ToString(),
      $"The UFC did not take the longitude of {name}"));
    TypeDigits(steps, waypoint.Longitude[..^4]);
    steps.Add(new(UfcButton(Enter, EnterDelay)));
    TypeDigits(steps, waypoint.Longitude[^4..]);
    steps.Add(Readback(
      UfcButton(Enter, EnterDelay),
      "^WYPT_Longitude$",
      CoordinateReadings(waypoint.LongitudeHemisphere, waypoint.Longitude),
      $"The AMPCD does not show the longitude entered for {name}"));
    steps.Add(Readback(
      null,
      "^WYPT_Latitude$",
      CoordinateReadings(waypoint.LatitudeHemisphere, waypoint.Latitude),
      $"The AMPCD does not show the latitude entered for {name}"));
    steps.Add(new(UfcButton(Option3, OptionDelay)));
    steps.Add(new(UfcButton(Option1, OptionDelay)));
    TypeDigits(steps, waypoint.ElevationFeet);
    steps.Add(Readback(
      UfcButton(Enter, ElevationEnterDelay),
      "^WYPT_Elev$",
      ElevationReadings(waypoint.ElevationFeet),
      $"The AMPCD does not show the elevation entered for {name}"));
  }

  private static CockpitStep SelectWaypoint(int number) => new(AmpcdButton(NextWaypoint))
  {
    Select = new NumberSelection(AmpcdIndicator, "^WYPT_Number$", number, AmpcdButton(PreviousWaypoint)),
    Tries = LastWaypoint + 1,
    Timeout = PageTimeout,
    Failure = $"Could not select waypoint {number} on the AMPCD",
  };

  private static CockpitStep PageStep(int button, IReadOnlyList<IndicationCheck> until, string failure, int tries = 1) =>
    new(AmpcdButton(button)) { Until = until, Tries = tries, Timeout = PageTimeout, Failure = failure };

  private static CockpitStep UfcStep(ButtonPress press, string element, string value, string failure) =>
    new(press) { Wait = [new(UfcIndicator, element, Values: [value])], Timeout = UfcTimeout, Failure = failure };

  private static CockpitStep Readback(ButtonPress? press, string element, IReadOnlyList<string> values, string failure) =>
    new(press) { Wait = [new(AmpcdIndicator, element, Values: values)], Timeout = ReadbackTimeout, Failure = failure };

  private static IReadOnlyList<string> CoordinateReadings(char hemisphere, string coordinate)
  {
    var parts = coordinate.Split('.');
    var tenThousandths = int.Parse(parts[0], CultureInfo.InvariantCulture) * 600_000
      + int.Parse(parts[1], CultureInfo.InvariantCulture) * 10_000
      + int.Parse(parts[2], CultureInfo.InvariantCulture);
    return [.. Neighbors(tenThousandths).Select(v => string.Create(
      CultureInfo.InvariantCulture,
      $"{hemisphere}{v / 600_000,3}°{v % 600_000 / 10_000:00}.{v % 10_000:0000}'"))];
  }

  private static IReadOnlyList<string> ElevationReadings(string feet)
  {
    var value = int.Parse(new string([.. feet.Where(char.IsAsciiDigit)]), CultureInfo.InvariantCulture);
    return [.. Neighbors(value).Select(v => string.Create(CultureInfo.InvariantCulture, $"{v}FT"))];
  }

  private static int[] Neighbors(int value) => [value, value - 1, value + 1];

  private static void TypeDigits(List<CockpitStep> steps, string text)
  {
    foreach (var character in text.Where(char.IsAsciiDigit)) steps.Add(new(Digit(character)));
  }

  private static ButtonPress Digit(char digit) => new(Ufc, DigitZero + digit - '0', ShortDelay);

  private static ButtonPress AmpcdButton(int code, int delay = ShortDelay) => new(Ampcd, code, delay);

  private static ButtonPress UfcButton(int code, int delay = ShortDelay) => new(Ufc, code, delay);
}
