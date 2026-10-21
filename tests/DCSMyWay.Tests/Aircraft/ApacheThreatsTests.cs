using DCSMyWay.Aircraft;

namespace DCSMyWay.Tests.Aircraft;

public class ApacheThreatsTests
{
  [Fact]
  public void AGenericTargetKeepsTheTgIdentifierAndHasNoRing()
  {
    Assert.Same(ApacheThreats.Target, ApacheThreats.Find(null));
    Assert.Equal("TG", ApacheThreats.Identifier(null));
    Assert.Equal(0, ApacheThreats.Target.EngagementMeters);
  }

  [Theory]
  [InlineData("8", "SA-8", 10_300)]
  [InlineData("15", "SA-15", 12_000)]
  [InlineData("ZU", "ZSU-23-4", 2_500)]
  [InlineData("S6", "2S6 Tunguska", 8_000)]
  [InlineData("SR", "Search radar", 0)]
  public void UsesTheApacheThreatCodesAndEngagementRings(string code, string name, int engagementMeters)
  {
    var threat = ApacheThreats.Find(code);

    Assert.Equal((code, name, engagementMeters), (threat.Code, threat.Name, threat.EngagementMeters));
    Assert.Equal(code, ApacheThreats.Identifier(code));
  }

  [Fact]
  public void FallsBackToAGenericTargetForUnknownCodes()
  {
    Assert.Same(ApacheThreats.Target, ApacheThreats.Find("XX"));
  }

  [Fact]
  public void ListsEveryThreatOnceStartingWithTheGenericTarget()
  {
    Assert.Same(ApacheThreats.Target, ApacheThreats.All[0]);
    Assert.Equal(ApacheThreats.All.Count, ApacheThreats.All.Select(t => t.Code).Distinct().Count());
  }
}
