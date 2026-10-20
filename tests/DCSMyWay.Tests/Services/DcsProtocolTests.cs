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
  public void SerializesWaypointsInTheFormatTheDcsScriptExpects()
  {
    ButtonPress[] presses = [new(17, 3032, 150, -1), new(20, 3026, 100, 0.2, AddDepress: false)];

    var command = DcsProtocol.CreateWaypointsCommand(7, presses);

    Assert.Equal(
      """{"type":"waypoints","id":7,"presses":[{"device":17,"code":3032,"delay":150,"activate":-1,"depress":true},{"device":20,"code":3026,"delay":100,"activate":0.2,"depress":false}]}""",
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
  public void SerializesAnEmptyRouteToClearTheMap()
  {
    Assert.Equal("""{"type":"route","points":[]}""", DcsProtocol.CreateRouteCommand([]));
  }
}
