using DCSMyWay.Aircraft;

namespace DCSMyWay.Tests.Aircraft;

public class MgrsTests
{
  [Theory]
  [InlineData(14.45, 2.88, "38T KM 5644 8074")]
  [InlineData(15.00, 3.00, "38T KM 5664 8175")]
  public void MatchesTheGridTheApacheComputesForTypedCoordinates(double latitudeMinutes, double longitudeMinutes, string expected)
  {
    var coordinate = Mgrs.FromLatLon(42 + latitudeMinutes / 60, 42 + longitudeMinutes / 60);

    Assert.Equal(expected, coordinate.Display);
  }

  [Fact]
  public void FormatsTheKeyboardEntryWithoutSpaces()
  {
    Assert.Equal("38TKM56448074", Mgrs.FromLatLon(42 + 14.45 / 60, 42 + 2.88 / 60).Entry);
  }

  [Fact]
  public void PlacesTheOriginInSquareAa()
  {
    Assert.Equal("31N AA 6602 0000", Mgrs.FromLatLon(0, 0).Display);
  }

  [Theory]
  [InlineData(60, 5, 32, 'V')]
  [InlineData(78, 15, 33, 'X')]
  [InlineData(78, 25, 35, 'X')]
  [InlineData(-51.7, -59.5, 21, 'F')]
  [InlineData(36.2, -115.0, 11, 'S')]
  public void PicksTheZoneAndBandIncludingTheNorwayAndSvalbardExceptions(double latitude, double longitude, int zone, char band)
  {
    var coordinate = Mgrs.FromLatLon(latitude, longitude);

    Assert.Equal((zone, band), (coordinate.Zone, coordinate.Band));
  }

  [Fact]
  public void UsesTheFalseNorthingInTheSouthernHemisphere()
  {
    var north = Mgrs.FromLatLon(0.0001, 3);
    var south = Mgrs.FromLatLon(-0.0001, 3);

    Assert.Equal(("EA", 1), (north.Square, north.Northing));
    Assert.Equal(("EV", 9999), (south.Square, south.Northing));
  }
}
