using System.Text.Json.Serialization;

namespace DCSMyWay.Models;

[JsonConverter(typeof(JsonStringEnumConverter<AppTheme>))]
public enum AppTheme
{
  System,
  Light,
  Dark,
}
