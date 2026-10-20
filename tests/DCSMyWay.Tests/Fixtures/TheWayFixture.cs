using System.Text.Json;
using DCSMyWay.Models;

namespace DCSMyWay.Tests.Fixtures;

public sealed record TheWayRawWaypoint(int Id, string Name, double Lat, double Long, double Elev)
{
  public Waypoint ToWaypoint() => new(Id, Name, Lat, Long, Elev);
}

public sealed record TheWayCockpitWaypoint(int Id, string Name, string Lat, string LatHem, string Long, string LongHem, string Elev)
{
  public CockpitWaypoint ToCockpitWaypoint() => new(Name, LatHem[0], Lat, LongHem[0], Long, Elev);
}

public sealed record TheWayButton(int Device, int Code, int Delay, double Activate, string AddDepress)
{
  public ButtonPress ToButtonPress() => new(Device, Code, Delay, Activate, AddDepress == "true");
}

public sealed record TheWayFixture(
  List<TheWayRawWaypoint>? Raw,
  List<TheWayCockpitWaypoint>? Formatted,
  List<TheWayCockpitWaypoint>? Waypoints,
  List<TheWayButton> Buttons)
{
  private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

  public static TheWayFixture Load(string fileName) =>
    JsonSerializer.Deserialize<TheWayFixture>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName)), Options)!;
}
