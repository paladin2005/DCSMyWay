using System.Text.Json.Serialization;

namespace DCSMyWay.Services;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(IncomingMessage))]
[JsonSerializable(typeof(WaypointsCommand))]
[JsonSerializable(typeof(CancelCommand))]
[JsonSerializable(typeof(CrosshairCommand))]
[JsonSerializable(typeof(StatusCommand))]
[JsonSerializable(typeof(RouteCommand))]
internal sealed partial class DcsJsonContext : JsonSerializerContext;
