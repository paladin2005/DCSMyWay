using System.Text;
using System.Text.RegularExpressions;

namespace DCSMyWay.Services;

public sealed partial class DcsScripts(string sourceFolder)
{
  public const string ExportLine =
    "pcall(function() local DCSMyWayLfs = require('lfs'); dofile(DCSMyWayLfs.writedir() .. 'Scripts/DCSMyWay/DCSMyWayExport.lua'); end)";

  public static readonly string DefaultSource = Path.Combine(AppContext.BaseDirectory, "DCS");
  public static readonly string ExportLua = Path.Combine("Scripts", "Export.lua");
  public static readonly string ExportScript = Path.Combine("Scripts", "DCSMyWay", "DCSMyWayExport.lua");
  public static readonly string Autoexec = Path.Combine("Config", "autoexec.cfg");

  private static readonly string[] AutoexecLines =
  [
    "if not net then net = {} end",
    "net.allow_unsafe_api = net.allow_unsafe_api or {}",
    "net.allow_unsafe_api[#net.allow_unsafe_api + 1] = \"userhooks\"",
    "net.allow_dostring_in = net.allow_dostring_in or {}",
    "net.allow_dostring_in[#net.allow_dostring_in + 1] = \"mission\"",
  ];

  public static bool NeedsDcsRestart(IEnumerable<string> changes) => changes.Any(c => c != ExportScript && c != ExportLua);

  public async Task<IReadOnlyList<string>> InstallAsync(string writeFolder)
  {
    var changes = new List<string>();
    foreach (var source in Directory.EnumerateFiles(sourceFolder, "*", SearchOption.AllDirectories))
    {
      var relative = Path.GetRelativePath(sourceFolder, source);
      var target = Path.Combine(writeFolder, relative);
      var bytes = await File.ReadAllBytesAsync(source);
      if (File.Exists(target) && (await File.ReadAllBytesAsync(target)).AsSpan().SequenceEqual(bytes)) continue;

      Directory.CreateDirectory(Path.GetDirectoryName(target)!);
      await File.WriteAllBytesAsync(target, bytes);
      changes.Add(relative);
    }

    var exportLua = Path.Combine(writeFolder, ExportLua);
    var exportText = await ReadAsync(exportLua);
    if (exportText is null || !SplitLines(exportText).Any(IsExportLine))
    {
      await AppendAsync(exportLua, exportText, [ExportLine]);
      changes.Add(ExportLua);
    }

    var autoexec = Path.Combine(writeFolder, Autoexec);
    var autoexecText = await ReadAsync(autoexec);
    if (autoexecText is null || !AllowsMapDrawing(autoexecText))
    {
      await AppendAsync(autoexec, autoexecText, AutoexecLines);
      changes.Add(Autoexec);
    }
    return changes;
  }

  public async Task RemoveAsync(string writeFolder)
  {
    var exportLua = Path.Combine(writeFolder, ExportLua);
    var exportText = await ReadAsync(exportLua);
    if (exportText is not null && SplitLines(exportText).Any(IsExportLine))
    {
      await RewriteAsync(exportLua, string.Concat(SplitLines(exportText).Where(l => !IsExportLine(l))));
    }

    var autoexec = Path.Combine(writeFolder, Autoexec);
    var autoexecText = await ReadAsync(autoexec);
    var block = autoexecText is null ? null : Block(autoexecText);
    if (block is not null) await RewriteAsync(autoexec, autoexecText!.Replace(block, ""));

    var sources = Directory.EnumerateFiles(sourceFolder, "*", SearchOption.AllDirectories).ToList();
    foreach (var source in sources)
    {
      var target = Path.Combine(writeFolder, Path.GetRelativePath(sourceFolder, source));
      if (File.Exists(target)) File.Delete(target);
    }
    var folders = sources
      .Select(s => Path.GetDirectoryName(Path.GetRelativePath(sourceFolder, s))!)
      .SelectMany(Ancestors)
      .Distinct()
      .OrderByDescending(f => f.Length);
    foreach (var folder in folders)
    {
      var target = Path.Combine(writeFolder, folder);
      if (Directory.Exists(target) && !Directory.EnumerateFileSystemEntries(target).Any()) Directory.Delete(target);
    }
  }

  private static IEnumerable<string> Ancestors(string folder)
  {
    for (var current = folder; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current)) yield return current;
  }

  private static bool IsExportLine(string line) => !line.TrimStart().StartsWith("--") && line.Contains("DCSMyWayExport.lua");

  private static bool AllowsMapDrawing(string text)
  {
    if (Block(text) is not null) return true;
    var code = LineComment().Replace(text, "");
    return UnsafeApiWithHooks().IsMatch(code) && DostringInMission().IsMatch(code);
  }

  private static string? Block(string text) =>
    new[] { "\r\n", "\n" }.Select(n => string.Join(n, AutoexecLines) + n).FirstOrDefault(text.Contains);

  private static string NewLine(string? text) => text is not null && !text.Contains("\r\n") && text.Contains('\n') ? "\n" : "\r\n";

  private static async Task<string?> ReadAsync(string path) =>
    File.Exists(path) ? Encoding.Latin1.GetString(await File.ReadAllBytesAsync(path)) : null;

  private static async Task AppendAsync(string path, string? text, IEnumerable<string> lines)
  {
    var newLine = NewLine(text);
    var separator = string.IsNullOrEmpty(text) || text.EndsWith('\n') ? "" : newLine;
    if (text is not null) BackUp(path);
    else Directory.CreateDirectory(Path.GetDirectoryName(path)!);

    await using var stream = new FileStream(path, FileMode.Append, FileAccess.Write);
    await stream.WriteAsync(Encoding.Latin1.GetBytes(separator + string.Concat(lines.Select(l => l + newLine))));
  }

  private static async Task RewriteAsync(string path, string text)
  {
    if (text.Length == 0)
    {
      File.Delete(path);
      return;
    }
    BackUp(path);
    await File.WriteAllBytesAsync(path, Encoding.Latin1.GetBytes(text));
  }

  private static void BackUp(string path) =>
    File.Copy(path, $"{path}.bak-dcsmyway-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}", true);

  private static string[] SplitLines(string text) => LineEnd().Split(text);

  [GeneratedRegex("(?<=\n)")]
  private static partial Regex LineEnd();

  [GeneratedRegex("--[^\r\n]*")]
  private static partial Regex LineComment();

  [GeneratedRegex("""allow_unsafe_api\s*=\s*\{[^}]*["']userhooks["']""")]
  private static partial Regex UnsafeApiWithHooks();

  [GeneratedRegex("""allow_dostring_in\s*=\s*\{[^}]*["']mission["']""")]
  private static partial Regex DostringInMission();
}
