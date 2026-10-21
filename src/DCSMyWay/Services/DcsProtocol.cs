using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using DCSMyWay.Models;

namespace DCSMyWay.Services;

public enum DcsMessageType
{
  Camera,
  Mark,
  Cockpit,
}

public sealed record DcsMessage(DcsMessageType Type, CameraState? Camera, CockpitState? Cockpit);

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

    if (incoming?.Type == "cockpit")
    {
      var cockpit = new CockpitState(incoming.Model, incoming.Steerpoint, incoming.Busy == true, incoming.TransferId, incoming.Done ?? 0, incoming.Failed, incoming.Seat);
      message = new DcsMessage(DcsMessageType.Cockpit, null, cockpit);
      return true;
    }

    DcsMessageType? type = incoming?.Type switch
    {
      "camera" => DcsMessageType.Camera,
      "mark" => DcsMessageType.Mark,
      _ => null,
    };
    if (type is null || incoming?.Model is null) return false;
    if (incoming.Lat is not { } latitude || !double.IsFinite(latitude)) return false;
    if (incoming.Lon is not { } longitude || !double.IsFinite(longitude)) return false;
    if (incoming.Elev is not { } elevation || !double.IsFinite(elevation)) return false;

    var position = new DcsPosition(incoming.Model, latitude, longitude, elevation);
    message = new DcsMessage(type.Value, new CameraState(position, incoming.Crosshair == true, incoming.AircraftId), null);
    return true;
  }

  public static string CreateWaypointsCommand(int transferId, IEnumerable<CockpitStep> steps)
  {
    var commands = steps.Select(CreateStepCommand).ToList();
    return JsonSerializer.Serialize(new WaypointsCommand("waypoints", transferId, commands), DcsJsonContext.Default.WaypointsCommand);
  }

  private static StepCommand CreateStepCommand(CockpitStep step) => new(
    step.Press?.Device,
    step.Press?.Code,
    step.Press?.Delay,
    step.Press?.Activate,
    step.Press?.AddDepress,
    CreateCheckCommands(step.Until),
    CreateCheckCommands(step.Wait),
    step.Select is { } select
      ? new SelectCommand(select.Indicator, select.Element, select.Target, CreatePressCommand(select.Decrease))
      : null,
    step.IsChecked ? step.Tries : null,
    step.IsChecked ? step.Timeout : null,
    step.Failure,
    step.Options?.Select(CreateOptionCommand).ToList(),
    step.Otherwise is { } otherwise ? CreatePressCommand(otherwise) : null,
    CreateCheckCommands(step.Unless));

  private static OptionCommand CreateOptionCommand(StepOption option) => new(
    CreateCheckCommand(option.When),
    option.Press?.Device,
    option.Press?.Code,
    option.Press?.Delay,
    option.Press?.Activate,
    option.Press?.AddDepress);

  private static PressCommand CreatePressCommand(ButtonPress press) =>
    new(press.Device, press.Code, press.Delay, press.Activate, press.AddDepress);

  private static List<CheckCommand>? CreateCheckCommands(IReadOnlyList<IndicationCheck>? checks) =>
    checks?.Select(CreateCheckCommand).ToList();

  private static CheckCommand CreateCheckCommand(IndicationCheck check) =>
    new(check.Indicator, check.Element, check.Pattern, check.Values?.ToList());

  public static string CreateCancelCommand() =>
    JsonSerializer.Serialize(new CancelCommand("cancel"), DcsJsonContext.Default.CancelCommand);

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

internal sealed record IncomingMessage(
  string? Type,
  string? Model,
  double? Lat,
  double? Lon,
  double? Elev,
  bool? Crosshair,
  bool? Busy,
  long? AircraftId,
  int? Steerpoint,
  int? TransferId,
  int? Done,
  string? Failed,
  int? Seat);

internal sealed record WaypointsCommand(string Type, int Id, List<StepCommand> Steps);

internal sealed record StepCommand(
  int? Device,
  int? Code,
  int? Delay,
  double? Activate,
  bool? Depress,
  List<CheckCommand>? Until,
  List<CheckCommand>? Wait,
  SelectCommand? Select,
  int? Tries,
  int? Timeout,
  string? Failure,
  List<OptionCommand>? Options,
  PressCommand? Otherwise,
  List<CheckCommand>? Unless);

internal sealed record OptionCommand(CheckCommand When, int? Device, int? Code, int? Delay, double? Activate, bool? Depress);

internal sealed record PressCommand(int Device, int Code, int Delay, double Activate, bool Depress);

internal sealed record CheckCommand(int Indicator, string Element, string? Pattern, List<string>? Values);

internal sealed record SelectCommand(int Indicator, string Element, int Target, PressCommand Down);

internal sealed record CancelCommand(string Type);

internal sealed record CrosshairCommand(string Type, bool Visible);

internal sealed record StatusCommand(string Type, string Text);

internal sealed record RouteCommand(string Type, List<RoutePoint> Points);

internal sealed record RoutePoint(string Name, double Lat, double Lon);
