using System.Globalization;
using DCSMyWay.Models;

namespace DCSMyWay.Aircraft;

public static class AH64
{
  private const int CpgSeat = 1;
  private const int TsdButton = 3029;
  private const int PointButton = 3013;
  private const int RouteButton = 3014;
  private const int L1 = 3024;
  private const int L2 = 3023;
  private const int L3 = 3022;
  private const int R1 = 3007;
  private const int Clear = 3001;
  private const int Backspace = 3002;
  private const int Enter = 3006;
  private const int Delay = 100;
  private const int PageTimeout = 1000;
  private const int KeyboardTimeout = 1500;
  private const int ReadbackTimeout = 2000;
  private const int RouteScrollTimeout = 300;
  private const int RouteScrolls = 30;
  private const int FreeTextLength = 3;
  private const int LastWaypoint = 50;
  private const string ScratchpadElement = "^Standby_text$";
  private const string AddIndexElement = "^ADDWaypointStatusWindowPOINT_text_1$";
  private const string AddTypeElement = "^ADDWaypointStatusWindowPOINT_text_2$";
  private const string ReviewIndexElement = "^ActiveWaypointStatusWindowPOINT_text_1$";
  private const string ReviewFreeTextElement = "^ActiveWaypointStatusWindowPOINT_text_3$";
  private const string ReviewMgrsElement = "^ActiveWaypointStatusWindowPOINT_text_8$";
  private const string ReviewAltitudeElement = "^ActiveWaypointStatusWindowPOINT_text_13$";
  private static readonly int[] RouteRowButtons = [3008, 3009, 3010, 3011];
  private static readonly string[] RouteRowElements = ["^PB8_%d+$", "^PB9_%d+$", "^PB10_%d+$", "^PB11_%d+$"];

  private static readonly CrewStation Pilot = new(MpdDevice: 43, KeyboardDevice: 29, MpdIndicator: 9, KeyboardIndicator: 16);
  private static readonly CrewStation Cpg = new(MpdDevice: 45, KeyboardDevice: 30, MpdIndicator: 13, KeyboardIndicator: 15);

  public static readonly IReadOnlyList<string> ModuleNames = ["AH-64D_BLK_II"];

  public static readonly SupportedAircraft Aircraft = new(
    "AH-64D",
    ModuleNames,
    "waypoint",
    "WPT",
    1,
    LastWaypoint,
    "Show the TSD in NAV phase on one of your MPDs so DCSMyWay can read the current waypoint",
    CreateTransfer);

  public static string FreeText(string name) =>
    new([.. name.Where(char.IsAsciiLetterOrDigit).Take(FreeTextLength).Select(char.ToUpperInvariant)]);

  public static string PointIndex(int number) => string.Create(CultureInfo.InvariantCulture, $"W{number:00}");

  public static TransferSequence CreateTransfer(IEnumerable<Waypoint> waypoints, int startWaypoint, int? seat)
  {
    var station = seat == CpgSeat ? Cpg : Pilot;
    var steps = new List<CockpitStep>
    {
      new(station.MpdButton(TsdButton)),
      station.PageStep(PointButton, "^PB13_%d+_b$", "The MPD did not open TSD > POINT"),
    };
    var waypointEnteredAfter = new List<int>();
    var numbers = new List<int>();
    var number = startWaypoint;
    foreach (var waypoint in waypoints)
    {
      EnterPoint(steps, station, waypoint, number);
      waypointEnteredAfter.Add(steps.Count);
      numbers.Add(number++);
    }

    AddToRoute(steps, station, numbers);
    steps.Add(new(station.MpdButton(TsdButton)));
    return new TransferSequence(steps, waypointEnteredAfter);
  }

  private static void EnterPoint(List<CockpitStep> steps, CrewStation station, Waypoint waypoint, int number)
  {
    var index = PointIndex(number);
    var freeText = FreeText(waypoint.Name);
    var coordinate = Mgrs.FromLatLon(waypoint.Latitude, waypoint.Longitude);
    var feet = CoordinateFormat.ToFeet(waypoint.ElevationMeters).TrimStart('-');
    var name = waypoint.Name;
    var addingPoint = station.Keyboard("^IDENT:");
    var editingText = station.Keyboard("^FREE:");

    steps.Add(new(station.MpdButton(L2))
    {
      Until = [station.Mpd("^PB24_%d+$", "IDENT>")],
      Timeout = PageTimeout,
      Failure = "The MPD did not open POINT > ADD",
    });
    steps.Add(new(station.MpdButton(L3))
    {
      Wait = [station.Mpd(AddTypeElement, "WP")],
      Timeout = PageTimeout,
      Failure = "The MPD did not select the WP point type",
    });
    steps.Add(new(null)
    {
      Options = [new(station.Mpd(AddIndexElement, index), station.MpdButton(L1)), new(station.Mpd("^PB24_%d+$", "IDENT>"), station.MpdButton(L2))],
      Timeout = PageTimeout,
      Failure = $"The MPD did not show the next free point while adding {index}",
    });
    steps.Add(new(null)
    {
      Wait = [addingPoint, station.Mpd("^PB22_%d+$", "EDIT")],
      Timeout = PageTimeout,
      Failure = $"The MPD did not get ready to enter {index}",
    });
    steps.Add(new(station.MpdButton(L1))
    {
      Unless = [addingPoint],
      Wait = [station.Keyboard("^POINT:")],
      Timeout = KeyboardTimeout,
      Failure = $"The KU did not ask for the point to edit for {name}",
    });
    Type(steps, station, index, [addingPoint]);
    steps.Add(new(station.KeyboardButton(Enter))
    {
      Unless = [addingPoint],
      Wait = [station.Mpd(ReviewIndexElement, index)],
      Timeout = KeyboardTimeout,
      Failure = $"{index} does not exist and is not the next free point",
    });
    steps.Add(new(station.MpdButton(L3))
    {
      Unless = [addingPoint],
      Wait = [station.Mpd("^PB24_%d+$", "FREE>")],
      Timeout = PageTimeout,
      Failure = $"The MPD did not edit {index}",
    });
    steps.Add(new(station.MpdButton(L1))
    {
      Unless = [addingPoint],
      Wait = [editingText],
      Timeout = KeyboardTimeout,
      Failure = $"The KU did not ask for the text of {index}",
    });
    steps.Add(new(station.KeyboardButton(Enter))
    {
      Unless = [editingText],
      Wait = [editingText],
      Timeout = KeyboardTimeout,
      Failure = $"The KU did not accept the WP identifier for {index}",
    });
    steps.Add(new(null)
    {
      Wait = [station.Mpd(AddIndexElement, index)],
      Timeout = PageTimeout,
      Failure = $"The MPD is not entering {index}",
    });
    for (var i = 0; i < FreeTextLength; i++) steps.Add(new(station.KeyboardButton(Backspace)));
    Type(steps, station, freeText, null);
    steps.Add(KeyboardEnter(station, "^UTM LAT/LONG:", $"The KU did not accept the text of {name}"));
    steps.Add(KeyboardClear(station, "^UTM LAT/LONG:#?$"));
    Type(steps, station, coordinate.Entry, null);
    steps.Add(KeyboardEnter(station, "^ALTITUDE:", $"The KU did not accept the coordinates of {name}"));
    steps.Add(KeyboardClear(station, "^ALTITUDE:#?$"));
    Type(steps, station, feet, null);
    steps.Add(new(station.KeyboardButton(Enter))
    {
      Wait = [station.Mpd(ReviewIndexElement, index)],
      Timeout = KeyboardTimeout,
      Failure = $"The KU did not accept the altitude of {name}",
    });
    steps.Add(Readback(station, ReviewMgrsElement, coordinate.Display, $"The MPD does not show the coordinates entered for {name}"));
    steps.Add(Readback(station, ReviewAltitudeElement, $"{feet} FT", $"The MPD does not show the altitude entered for {name}"));
    steps.Add(Readback(station, ReviewFreeTextElement, freeText, $"The MPD does not show the text entered for {name}"));
  }

  private static void AddToRoute(List<CockpitStep> steps, CrewStation station, List<int> numbers)
  {
    steps.Add(station.PageStep(RouteButton, "^PB14_%d+_b$", "The MPD did not open TSD > RTE"));
    foreach (var number in numbers)
    {
      var index = PointIndex(number);
      var inRoute = string.Create(CultureInfo.InvariantCulture, $"^..{number:00}$");
      steps.Add(new(station.MpdButton(L2))
      {
        Until = [station.Mpd("^PB23_%d+_b$")],
        Timeout = PageTimeout,
        Failure = "The MPD did not open RTE > ADD",
      });
      steps.Add(new(station.MpdButton(L1))
      {
        Wait = [station.Keyboard("^POINT:")],
        Timeout = KeyboardTimeout,
        Failure = $"The KU did not ask for the point to add {index} to the route",
      });
      Type(steps, station, index, null);
      steps.Add(new(station.KeyboardButton(Enter))
      {
        Wait = [station.Mpd("^PB24_%d+$", index)],
        Timeout = KeyboardTimeout,
        Failure = $"The MPD did not take {index} to add it to the route",
      });
      steps.Add(new(null)
      {
        Options =
        [
          .. RouteRowElements.Select(row => new StepOption(new(station.MpdIndicator, row, inRoute), station.MpdButton(L2))),
          .. RouteRowElements.Select((row, i) => new StepOption(station.Mpd(row, "END"), station.MpdButton(RouteRowButtons[i]))),
        ],
        Otherwise = station.MpdButton(R1),
        Tries = RouteScrolls,
        Timeout = RouteScrollTimeout,
        Failure = $"Could not find the end of the route to add {index}",
      });
      steps.Add(new(null)
      {
        Wait = [.. RouteRowElements.Select(row => new IndicationCheck(station.MpdIndicator, row, inRoute))],
        Timeout = PageTimeout,
        Failure = $"{index} did not appear in the route",
      });
    }
  }

  private static CockpitStep KeyboardEnter(CrewStation station, string nextPrompt, string failure) =>
    new(station.KeyboardButton(Enter)) { Wait = [station.Keyboard(nextPrompt)], Timeout = KeyboardTimeout, Failure = failure };

  private static CockpitStep KeyboardClear(CrewStation station, string clearedPrompt) =>
    new(station.KeyboardButton(Clear)) { Wait = [station.Keyboard(clearedPrompt)], Timeout = KeyboardTimeout, Failure = "The KU did not clear its scratchpad" };

  private static CockpitStep Readback(CrewStation station, string element, string value, string failure) =>
    new(null) { Wait = [station.Mpd(element, value)], Timeout = ReadbackTimeout, Failure = failure };

  private static void Type(List<CockpitStep> steps, CrewStation station, string text, IReadOnlyList<IndicationCheck>? unless)
  {
    foreach (var code in text.Select(KeyCode).OfType<int>()) steps.Add(new(station.KeyboardButton(code)) { Unless = unless });
  }

  private static int? KeyCode(char character) => character switch
  {
    >= 'A' and <= 'Z' => 3007 + character - 'A',
    >= '1' and <= '9' => 3033 + character - '1',
    '0' => 3043,
    _ => null,
  };

  private sealed record CrewStation(int MpdDevice, int KeyboardDevice, int MpdIndicator, int KeyboardIndicator)
  {
    public ButtonPress MpdButton(int code) => new(MpdDevice, code, Delay);

    public ButtonPress KeyboardButton(int code) => new(KeyboardDevice, code, Delay);

    public IndicationCheck Mpd(string element, string? value = null) =>
      new(MpdIndicator, element, Values: value is null ? null : [value]);

    public IndicationCheck Keyboard(string pattern) => new(KeyboardIndicator, ScratchpadElement, pattern);

    public CockpitStep PageStep(int button, string page, string failure) =>
      new(MpdButton(button)) { Until = [Mpd(page)], Tries = 2, Timeout = PageTimeout, Failure = failure };
  }
}
