using System.Globalization;

namespace DCSMyWay.Aircraft;

public static class CoordinateFormat
{
  private const double FeetPerMeter = 3.280839895;
  private const double RoundTripTolerance = 1e-6;

  public static char LatitudeHemisphere(double latitude) => latitude > 0 ? 'N' : 'S';

  public static char LongitudeHemisphere(double longitude) => longitude > 0 ? 'E' : 'W';

  public static string ToDegreesMinutes(double coordinate, int degreeDigits, int minuteDecimals)
  {
    var format = $"F{minuteDecimals}";
    var absoluteCoordinate = Math.Abs(coordinate);
    var degrees = (int)absoluteCoordinate;
    var minutes = ((absoluteCoordinate - degrees) * 60).ToString(format, CultureInfo.InvariantCulture);
    if (minutes.StartsWith("60"))
    {
      degrees++;
      minutes = 0.0.ToString(format, CultureInfo.InvariantCulture);
    }
    return $"{degrees.ToString(CultureInfo.InvariantCulture).PadLeft(degreeDigits, '0')}.{minutes.PadLeft(minuteDecimals + 3, '0')}";
  }

  public static string ToFeet(double meters) =>
    ((long)Math.Truncate(meters * FeetPerMeter + RoundTripTolerance)).ToString(CultureInfo.InvariantCulture);

  public static double ToMeters(double feet) => feet / FeetPerMeter;
}
