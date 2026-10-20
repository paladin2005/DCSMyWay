using System.Text.Json.Serialization;
using DCSMyWay.Models;

namespace DCSMyWay.Services;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class AppJsonContext : JsonSerializerContext;
