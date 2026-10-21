using DCSMyWay.Interop;
using Microsoft.Win32;

namespace DCSMyWay.Services;

public static class DcsFolders
{
  private const string DefaultFolder = "DCS";

  public static string SavedGames =>
    NativeMethods.SHGetKnownFolderPath(NativeMethods.SavedGamesFolder, 0, 0, out var path) == 0
      ? path
      : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Saved Games");

  public static Task<IReadOnlyList<string>> FindAsync() => FindAsync(SavedGames, InstallPaths());

  public static async Task<IReadOnlyList<string>> FindAsync(string savedGames, IEnumerable<string> installPaths)
  {
    var installed = new List<string>();
    foreach (var installPath in installPaths)
    {
      var folder = Path.Combine(savedGames, await FolderNameAsync(installPath));
      if (IsWriteFolder(folder) && !installed.Contains(folder, StringComparer.OrdinalIgnoreCase)) installed.Add(folder);
    }
    if (installed.Count > 0) return installed;
    if (!Directory.Exists(savedGames)) return [];

    return [.. Directory.EnumerateDirectories(savedGames, $"{DefaultFolder}*")
      .Where(f => Path.GetFileName(f) is var name
        && (name.Equals(DefaultFolder, StringComparison.OrdinalIgnoreCase) || name.StartsWith($"{DefaultFolder}.", StringComparison.OrdinalIgnoreCase)))
      .Where(IsWriteFolder)
      .Order(StringComparer.OrdinalIgnoreCase)];
  }

  private static async Task<string> FolderNameAsync(string installPath)
  {
    var variantPath = Path.Combine(installPath, "dcs_variant.txt");
    var variant = File.Exists(variantPath) ? (await File.ReadAllTextAsync(variantPath)).Trim() : "";
    return variant.Length == 0 ? DefaultFolder : $"{DefaultFolder}.{variant}";
  }

  private static bool IsWriteFolder(string folder) => Directory.Exists(Path.Combine(folder, "Config"));

  private static List<string> InstallPaths()
  {
    using var root = Registry.CurrentUser.OpenSubKey(@"Software\Eagle Dynamics");
    if (root is null) return [];

    var paths = new List<string>();
    foreach (var name in root.GetSubKeyNames())
    {
      using var key = root.OpenSubKey(name);
      if (key?.GetValue("Path") is string path && Directory.Exists(path)) paths.Add(path);
    }
    return paths;
  }
}
