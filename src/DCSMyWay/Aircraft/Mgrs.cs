using System.Globalization;

namespace DCSMyWay.Aircraft;

public sealed record MgrsCoordinate(int Zone, char Band, string Square, int Easting, int Northing)
{
  public string Entry => string.Create(CultureInfo.InvariantCulture, $"{Zone}{Band}{Square}{Easting:0000}{Northing:0000}");

  public string Display => string.Create(CultureInfo.InvariantCulture, $"{Zone}{Band} {Square} {Easting:0000} {Northing:0000}");
}

public static class Mgrs
{
  private const double SemiMajorAxis = 6_378_137.0;
  private const double Flattening = 1 / 298.257223563;
  private const double ScaleFactor = 0.9996;
  private const double FalseEasting = 500_000;
  private const double SouthernFalseNorthing = 10_000_000;
  private const int Precision = 10;
  private const int SquareSize = 100_000;
  private const string Bands = "CDEFGHJKLMNPQRSTUVWX";
  private const string RowLetters = "ABCDEFGHJKLMNPQRSTUV";
  private static readonly string[] ColumnLetters = ["ABCDEFGH", "JKLMNPQR", "STUVWXYZ"];

  public static MgrsCoordinate FromLatLon(double latitude, double longitude)
  {
    var zone = Zone(latitude, longitude);
    var (easting, northing) = ToUtm(latitude, longitude, zone);
    var roundedEasting = RoundToPrecision(easting);
    var roundedNorthing = RoundToPrecision(northing);
    var column = ColumnLetters[(zone - 1) % 3][roundedEasting / SquareSize - 1];
    var row = RowLetters[(roundedNorthing / SquareSize + (zone % 2 == 0 ? 5 : 0)) % RowLetters.Length];
    return new MgrsCoordinate(
      zone,
      Band(latitude),
      $"{column}{row}",
      roundedEasting % SquareSize / Precision,
      roundedNorthing % SquareSize / Precision);
  }

  private static int RoundToPrecision(double meters) =>
    (int)Math.Round(meters / Precision, MidpointRounding.AwayFromZero) * Precision;

  private static int Zone(double latitude, double longitude)
  {
    if (latitude is >= 56 and < 64 && longitude is >= 3 and < 12) return 32;
    if (latitude is >= 72 and < 84 && longitude >= 0 && longitude < 42) return longitude switch
    {
      < 9 => 31,
      < 21 => 33,
      < 33 => 35,
      _ => 37,
    };
    return Math.Min((int)Math.Floor((longitude + 180) / 6) + 1, 60);
  }

  private static char Band(double latitude) =>
    Bands[Math.Clamp((int)Math.Floor((latitude + 80) / 8), 0, Bands.Length - 1)];

  private static (double Easting, double Northing) ToUtm(double latitude, double longitude, int zone)
  {
    var e2 = Flattening * (2 - Flattening);
    var ep2 = e2 / (1 - e2);
    var phi = double.DegreesToRadians(latitude);
    var lambda = double.DegreesToRadians(longitude);
    var lambda0 = double.DegreesToRadians((zone - 1) * 6 - 180 + 3);
    var sinPhi = Math.Sin(phi);
    var n = SemiMajorAxis / Math.Sqrt(1 - e2 * sinPhi * sinPhi);
    var t = Math.Tan(phi) * Math.Tan(phi);
    var c = ep2 * Math.Cos(phi) * Math.Cos(phi);
    var a = Math.Cos(phi) * (lambda - lambda0);
    var m = SemiMajorAxis * (
      (1 - e2 / 4 - 3 * e2 * e2 / 64 - 5 * e2 * e2 * e2 / 256) * phi
      - (3 * e2 / 8 + 3 * e2 * e2 / 32 + 45 * e2 * e2 * e2 / 1024) * Math.Sin(2 * phi)
      + (15 * e2 * e2 / 256 + 45 * e2 * e2 * e2 / 1024) * Math.Sin(4 * phi)
      - 35 * e2 * e2 * e2 / 3072 * Math.Sin(6 * phi));
    var easting = ScaleFactor * n * (a + (1 - t + c) * Math.Pow(a, 3) / 6
      + (5 - 18 * t + t * t + 72 * c - 58 * ep2) * Math.Pow(a, 5) / 120) + FalseEasting;
    var northing = ScaleFactor * (m + n * Math.Tan(phi) * (a * a / 2
      + (5 - t + 9 * c + 4 * c * c) * Math.Pow(a, 4) / 24
      + (61 - 58 * t + t * t + 600 * c - 330 * ep2) * Math.Pow(a, 6) / 720));
    return (easting, latitude < 0 ? northing + SouthernFalseNorthing : northing);
  }
}
