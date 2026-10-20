using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using DCSMyWay.Models;

namespace DCSMyWay.Services;

public enum DcsMessageType
{
  Camera,
  Mark,
}

public sealed record DcsMessage(DcsMessageType Type, DcsState State);

public static class DcsProtocol
{
  public static bool TryParseMessage(ReadOnlySpan<byte> json, [NotNullWhen(true)] out DcsMessage? message)
  {
    message = null;
    IncomingMessage? incoming;
    try
    {
      incoming = JsonSerializer.Deserialize(json, DcsJsonContext.Default.IncomingMessage);
    }
    catch (JsonException)
    {
      return false;
    }

    if (incoming?.Model is null) return false;
    if (incoming.Lat is not { } latitude || !double.IsFinite(latitude)) return false;
    if (incoming.Lon is not { } longitude || !double.IsFinite(longitude)) return false;
    if (incoming.Elev is not { } elevation || !double.IsFinite(elevation)) return false;
    DcsMessageType? type = incoming.Type switch
    {
      "camera" => DcsMessageType.Camera,
      "mark" => DcsMessageType.Mark,
      _ => null,
    };
    if (type is null) return false;

    var position = new DcsPosition(incoming.Model, latitude, longitude, elevation);
    message = new DcsMessage(type.Value, new DcsState(position, incoming.Crosshair == true, incoming.Busy == true));
    return true;
  }

  public static string CreateWaypointsCommand(IEnumerable<ButtonPress> presses)
  {
    var commands = presses
      .Select(p => new ButtonPressCommand(p.Device, p.Code, p.Delay, p.Activate, p.AddDepress))
      .ToList();
    return JsonSerializer.Serialize(new WaypointsCommand("waypoints", commands), DcsJsonContext.Default.WaypointsCommand);
  }

  public static string CreateCrosshairCommand(bool visible) =>
    JsonSerializer.Serialize(new CrosshairCommand("crosshair", visible), DcsJsonContext.Default.CrosshairCommand);

  public static string CreateStatusCommand(string text) =>
    JsonSerializer.Serialize(new StatusCommand("status", text), DcsJsonContext.Default.StatusCommand);

  public static string CreateRouteCommand(IEnumerable<Waypoint> waypoints)
  {
    var points = waypoints.Select(w => new RoutePoint(w.Name, w.Latitude, w.Longitude)).ToList();
    return JsonSerializer.Serialize(new RouteCommand("route", points), DcsJsonContext.Default.RouteCommand);
  }
}

internal sealed record IncomingMessage(string? Type, string? Model, double? Lat, double? Lon, double? Elev, bool? Crosshair, bool? Busy);

internal sealed record WaypointsCommand(string Type, List<ButtonPressCommand> Presses);

internal sealed record ButtonPressCommand(int Device, int Code, int Delay, double Activate, bool Depress);

internal sealed record CrosshairCommand(string Type, bool Visible);

internal sealed record StatusCommand(string Type, string Text);

internal sealed record RouteCommand(string Type, List<RoutePoint> Points);

internal sealed record RoutePoint(string Name, double Lat, double Lon);
