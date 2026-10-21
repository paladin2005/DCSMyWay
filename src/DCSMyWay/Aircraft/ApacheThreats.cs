using WinRT;

namespace DCSMyWay.Aircraft;

[GeneratedBindableCustomProperty]
public sealed partial record ApacheThreat(string? Code, string Name, int EngagementMeters);

public static class ApacheThreats
{
  public const string TargetIdentifier = "TG";

  public static readonly ApacheThreat Target = new(null, "Target", 0);

  public static IReadOnlyList<ApacheThreat> All { get; } =
  [
    Target,
    new("2", "SA-2", 43_000),
    new("3", "SA-3", 18_000),
    new("6", "SA-6", 25_000),
    new("8", "SA-8", 10_300),
    new("9", "SA-9", 5_000),
    new("10", "SA-10", 99_900),
    new("11", "SA-11", 35_000),
    new("13", "SA-13/19", 5_000),
    new("15", "SA-15", 12_000),
    new("16", "SA-16/18", 5_200),
    new("S6", "2S6 Tunguska", 8_000),
    new("ZU", "ZSU-23-4", 2_500),
    new("GP", "Gepard", 4_000),
    new("VU", "Vulcan", 2_000),
    new("AA", "AAA", 6_000),
    new("GU", "Air defense", 5_000),
    new("HK", "Hawk", 45_000),
    new("PT", "Patriot", 99_900),
    new("RO", "Roland", 8_000),
    new("RA", "Rapier", 6_800),
    new("CH", "Chaparral", 8_500),
    new("ST", "Stinger", 4_500),
    new("SR", "Search radar", 0),
    new("TR", "Acquisition radar", 0),
  ];

  public static ApacheThreat Find(string? code) => All.FirstOrDefault(t => t.Code == code) ?? Target;

  public static string Identifier(string? code) => Find(code).Code ?? TargetIdentifier;
}
