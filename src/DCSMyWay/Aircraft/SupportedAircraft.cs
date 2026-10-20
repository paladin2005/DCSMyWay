using DCSMyWay.Models;

namespace DCSMyWay.Aircraft;

public sealed record SupportedAircraft(
  string Name,
  IReadOnlyList<string> ModuleNames,
  string SteerpointName,
  string SteerpointLabel,
  int FirstSteerpoint,
  int LastSteerpoint,
  string SteerpointHint,
  Func<IEnumerable<Waypoint>, int, TransferSequence> CreateTransfer)
{
  public static IReadOnlyList<SupportedAircraft> All => [F16.Aircraft, F18.Aircraft, A10.Aircraft];

  public static string Names => $"{string.Join(", ", All.SkipLast(1).Select(a => a.Name))} and {All[^1].Name}";

  public static SupportedAircraft? Find(string? module) =>
    module is null ? null : All.FirstOrDefault(a => a.ModuleNames.Contains(module));
}
