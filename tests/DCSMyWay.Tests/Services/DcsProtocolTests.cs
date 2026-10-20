using System.Text;
using DCSMyWay.Models;
using DCSMyWay.Services;

namespace DCSMyWay.Tests.Services;

public class DcsProtocolTests
{
  [Fact]
  public void ParsesTheCameraMessageSentByTheDcsScript()
  {
    var json = """{"type":"camera","model":"F-16C_50","lat":41.905925545,"lon":43.783457188,"elev":1677.665,"crosshair":true,"busy":false,"aircraftId":16777472,"steerpoint":3,"transferId":7,"done":42}""";

    var parsed = DcsProtocol.TryParseMessage(Encoding.UTF8.GetBytes(json), out var message);

    Assert.True(parsed);
    var position = new DcsPosition("F-16C_50", 41.905925545, 43.783457188, 1677.665);
    Assert.Equal(new DcsMessage(DcsMessageType.Camera, new DcsState(position, true, false, 16777472, 3, 7, 42)), message);
  }

  [Fact]
  public void ParsesTheMarkMessageSentByTheAcceptButton()
  {
    var json = """{"type":"mark","model":"F-16C_50","lat":-34.5,"lon":-58.25,"elev":12.5,"crosshair":true,"busy":true}""";

    var parsed = DcsProtocol.TryParseMessage(Encoding.UTF8.GetBytes(json), out var message);

    Assert.True(parsed);
    Assert.Equal(DcsMessageType.Mark, message!.Type);
    Assert.Equal(new DcsPosition("F-16C_50", -34.5, -58.25, 12.5), message.State.Position);
    Assert.True(message.State.IsEnteringWaypoints);
  }

  [Theory]
  [InlineData(""","aircraftId":null,"steerpoint":null,"transferId":null,"done":0""")]
  [InlineData("")]
  public void HasNoAircraftSteerpointOrTransferWhenDcsDoesNotReportThem(string optionalFields)
  {
    var json = $$"""{"type":"camera","model":"Spectator","lat":1,"lon":2,"elev":3,"crosshair":false,"busy":false{{optionalFields}}}""";

    var parsed = DcsProtocol.TryParseMessage(Encoding.UTF8.GetBytes(json), out var message);

    Assert.True(parsed);
    Assert.Null(message!.State.AircraftId);
    Assert.Null(message.State.ActiveSteerpoint);
    Assert.Null(message.State.TransferId);
    Assert.Equal(0, message.State.PressesDone);
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
