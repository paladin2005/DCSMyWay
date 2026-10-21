using System.Text;
using DCSMyWay.Models;
using DCSMyWay.Services;

namespace DCSMyWay.Tests.Services;

public class DcsProtocolTests
{
  [Fact]
  public void ParsesTheCameraMessageSentByTheHook()
  {
    var json = """{"type":"camera","model":"F-16C_50","lat":41.905925545,"lon":43.783457188,"elev":1677.665,"crosshair":true,"aircraftId":16777472}""";

    var parsed = DcsProtocol.TryParseMessage(Encoding.UTF8.GetBytes(json), out var message);

    Assert.True(parsed);
    var position = new DcsPosition("F-16C_50", 41.905925545, 43.783457188, 1677.665);
    Assert.Equal(new DcsMessage(DcsMessageType.Camera, new CameraState(position, true, 16777472), null), message);
  }

  [Fact]
  public void ParsesTheMarkMessageSentByTheAcceptButton()
  {
    var json = """{"type":"mark","model":"F-16C_50","lat":-34.5,"lon":-58.25,"elev":12.5,"crosshair":true,"aircraftId":null}""";

    var parsed = DcsProtocol.TryParseMessage(Encoding.UTF8.GetBytes(json), out var message);

    Assert.True(parsed);
    Assert.Equal(DcsMessageType.Mark, message!.Type);
    Assert.Equal(new DcsPosition("F-16C_50", -34.5, -58.25, 12.5), message.Camera!.Position);
    Assert.Null(message.Camera.AircraftId);
  }

  [Fact]
  public void ParsesTheCockpitMessageSentByTheExportScript()
  {
    var json = """{"type":"cockpit","model":"F-16C_50","steerpoint":3,"busy":true,"transferId":7,"done":42}""";

    var parsed = DcsProtocol.TryParseMessage(Encoding.UTF8.GetBytes(json), out var message);

    Assert.True(parsed);
    Assert.Equal(new DcsMessage(DcsMessageType.Cockpit, null, new CockpitState("F-16C_50", 3, true, 7, 42)), message);
  }

  [Fact]
  public void ParsesACockpitMessageWithoutAircraftSteerpointOrTransfer()
  {
    var json = """{"type":"cockpit","model":null,"steerpoint":null,"busy":false,"transferId":null,"done":0}""";

    var parsed = DcsProtocol.TryParseMessage(Encoding.UTF8.GetBytes(json), out var message);

    Assert.True(parsed);
    Assert.Equal(new CockpitState(null, null, false, null, 0), message!.Cockpit);
  }

  [Theory]
  [InlineData("not json")]
  [InlineData("{}")]
  [InlineData("""{"type":"camera","model":"F-16C_50","lon":43.7,"elev":10}""")]
  [InlineData("""{"type":"camera","lat":41.9,"lon":43.7,"elev":10}""")]
  [InlineData("""{"type":"other","model":"F-16C_50","lat":41.9,"lon":43.7,"elev":10}""")]
  [InlineData("""{"type":"camera","model":"F-16C_50","lat":nan,"lon":43.7,"elev":10}""")]
  [InlineData("""{"type":"camera","model":"F-16C_50","lat":41.9,"lon":-nan(ind),"elev":10}""")]
  [InlineData("""{"type":"camera","model":"F-16C_50","lat":41.9,"lon":43.7,"elev":inf}""")]
  public void RejectsMalformedMessages(string json)
  {
    Assert.False(DcsProtocol.TryParseMessage(Encoding.UTF8.GetBytes(json), out _));
  }

  [Fact]
  public void ParsesTheFailureOfTheLastTransfer()
  {
    var json = """{"type":"cockpit","model":"FA-18C_hornet","steerpoint":4,"busy":false,"transferId":3,"done":17,"failed":"PRECISE did not turn on"}""";

    var parsed = DcsProtocol.TryParseMessage(Encoding.UTF8.GetBytes(json), out var message);

    Assert.True(parsed);
    Assert.Equal(new CockpitState("FA-18C_hornet", 4, false, 3, 17, "PRECISE did not turn on"), message!.Cockpit);
  }

  [Fact]
  public void ParsesTheSeatOfTwoSeatAircraft()
  {
    var json = """{"type":"cockpit","model":"AH-64D_BLK_II","steerpoint":2,"busy":false,"transferId":null,"done":0,"failed":null,"seat":1}""";

    var parsed = DcsProtocol.TryParseMessage(Encoding.UTF8.GetBytes(json), out var message);

    Assert.True(parsed);
    Assert.Equal(new CockpitState("AH-64D_BLK_II", 2, false, null, 0, null, 1), message!.Cockpit);
  }

  [Fact]
  public void SerializesStepsThatAreSkippedWhenTheDisplayShowsSomething()
  {
    CockpitStep[] steps = [new(new(29, 3006, 100)) { Unless = [new(16, "^Standby_text$", "^FREE:")] }];

    var command = DcsProtocol.CreateWaypointsCommand(6, steps);

    Assert.Equal(
      """{"type":"waypoints","id":6,"steps":[{"device":29,"code":3006,"delay":100,"activate":1,"depress":true,"unless":[{"indicator":16,"element":"^Standby_text$","pattern":"^FREE:"}]}]}""",
      command);
  }

  [Fact]
  public void SerializesPlainButtonPressesInTheFormatTheDcsScriptExpects()
  {
    CockpitStep[] steps = [new(new(17, 3032, 150, -1)), new(new(20, 3026, 100, 0.2, AddDepress: false))];

    var command = DcsProtocol.CreateWaypointsCommand(7, steps);

    Assert.Equal(
      """{"type":"waypoints","id":7,"steps":[{"device":17,"code":3032,"delay":150,"activate":-1,"depress":true},{"device":20,"code":3026,"delay":100,"activate":0.2,"depress":false}]}""",
      command);
  }

  [Fact]
  public void SerializesStepsThatReadTheCockpitDisplays()
  {
    CockpitStep[] steps =
    [
      new(new(37, 3028, 100)) { Until = [new(4, "^Menu_SUPT$"), new(4, "^AC_Latitude$", "%.%d+'$")], Tries = 3, Timeout = 1000, Failure = "No menu" },
      new(null) { Wait = [new(4, "^WYPT_Latitude$", Values: ["N 42°14.6479'"])], Timeout = 3000, Failure = "Wrong latitude" },
      new(new(37, 3022, 100)) { Select = new(4, "^WYPT_Number$", 5, new(37, 3023, 100)), Tries = 60, Timeout = 1000 },
    ];

    var command = DcsProtocol.CreateWaypointsCommand(2, steps);

    Assert.Equal(
      """{"type":"waypoints","id":2,"steps":[""" +
      """{"device":37,"code":3028,"delay":100,"activate":1,"depress":true,"until":[{"indicator":4,"element":"^Menu_SUPT$"},{"indicator":4,"element":"^AC_Latitude$","pattern":"%.%d\u002B\u0027$"}],"tries":3,"timeout":1000,"failure":"No menu"},""" +
      """{"wait":[{"indicator":4,"element":"^WYPT_Latitude$","values":["N 42\u00B014.6479\u0027"]}],"tries":1,"timeout":3000,"failure":"Wrong latitude"},""" +
      """{"device":37,"code":3022,"delay":100,"activate":1,"depress":true,"select":{"indicator":4,"element":"^WYPT_Number$","target":5,"down":{"device":37,"code":3023,"delay":100,"activate":1,"depress":true}},"tries":60,"timeout":1000}""" +
      """]}""",
      command);
  }

  [Fact]
  public void SerializesStepsThatChooseAButtonByWhatTheDisplayShows()
  {
    CockpitStep[] steps =
    [
      new(null)
      {
        Options = [new(new(3, "^ActiveFP1$", Values: ["*"]), new(9, 3005, 100)), new(new(3, "^FPBUILDPageNumber$", "^1/"))],
        Otherwise = new(9, 3063, 100, -1),
        Tries = 7,
        Timeout = 300,
        Failure = "No plan",
      },
    ];

    var command = DcsProtocol.CreateWaypointsCommand(4, steps);

    Assert.Equal(
      """{"type":"waypoints","id":4,"steps":[{"tries":7,"timeout":300,"failure":"No plan","options":[""" +
      """{"when":{"indicator":3,"element":"^ActiveFP1$","values":["*"]},"device":9,"code":3005,"delay":100,"activate":1,"depress":true},""" +
      """{"when":{"indicator":3,"element":"^FPBUILDPageNumber$","pattern":"^1/"}}],"otherwise":""" +
      """{"device":9,"code":3063,"delay":100,"activate":-1,"depress":true}}]}""",
      command);
  }

  [Theory]
  [InlineData(true, """{"type":"crosshair","visible":true}""")]
  [InlineData(false, """{"type":"crosshair","visible":false}""")]
  public void SerializesTheCrosshairCommand(bool visible, string expected)
  {
    Assert.Equal(expected, DcsProtocol.CreateCrosshairCommand(visible));
  }

  [Fact]
  public void SerializesTheCancelCommand()
  {
    Assert.Equal("""{"type":"cancel"}""", DcsProtocol.CreateCancelCommand());
  }

  [Fact]
  public void SerializesTheStatusCommand()
  {
    Assert.Equal("""{"type":"status","text":"W3 added"}""", DcsProtocol.CreateStatusCommand("W3 added"));
  }

  [Fact]
  public void SerializesTheRouteCommandInWaypointOrder()
  {
    Waypoint[] waypoints = [new(1, "W1", 41.905925545, 43.783457188, 1677.665), new(3, "W3", -34.5, -58.25, 12)];

    var command = DcsProtocol.CreateRouteCommand(waypoints);

    Assert.Equal(
      """{"type":"route","points":[{"name":"W1","lat":41.905925545,"lon":43.783457188},{"name":"W3","lat":-34.5,"lon":-58.25}]}""",
      command);
  }

  [Fact]
  public void FlagsTargetsInTheRouteCommand()
  {
    Waypoint[] waypoints = [new(1, "W1", 41.5, 43.5, 10), new(2, "TGT", 41.6, 43.6, 12, IsTarget: true)];

    var command = DcsProtocol.CreateRouteCommand(waypoints);

    Assert.Equal(
      """{"type":"route","points":[{"name":"W1","lat":41.5,"lon":43.5},{"name":"TGT","lat":41.6,"lon":43.6,"target":true}]}""",
      command);
  }

  [Fact]
  public void SendsTheThreatNameAndEngagementRingOfThreats()
  {
    Waypoint[] waypoints =
    [
      new(1, "T1", 41.5, 43.5, 10, IsTarget: true, Threat: "8"),
      new(2, "T2", 41.6, 43.6, 10, IsTarget: true, Threat: "SR"),
      new(3, "W3", 41.7, 43.7, 10, Threat: "8"),
    ];

    var command = DcsProtocol.CreateRouteCommand(waypoints);

    Assert.Equal(
      """{"type":"route","points":[{"name":"T1 SA-8","lat":41.5,"lon":43.5,"target":true,"ring":10300},""" +
      """{"name":"T2 Search radar","lat":41.6,"lon":43.6,"target":true},{"name":"W3","lat":41.7,"lon":43.7}]}""",
      command);
  }

  [Fact]
  public void SerializesAnEmptyRouteToClearTheMap()
  {
    Assert.Equal("""{"type":"route","points":[]}""", DcsProtocol.CreateRouteCommand([]));
  }
}
