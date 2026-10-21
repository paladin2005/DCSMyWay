using DCSMyWay.Services;

namespace DCSMyWay.Tests.Services;

public sealed class DcsFoldersTests : IDisposable
{
  private readonly string _directory = Path.Combine(Path.GetTempPath(), $"DCSMyWay-tests-{Guid.NewGuid()}");

  private string SavedGames => Path.Combine(_directory, "Saved Games");

  public void Dispose()
  {
    if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
  }

  private string WriteFolder(string name, bool hasConfig = true)
  {
    var folder = Path.Combine(SavedGames, name);
    Directory.CreateDirectory(hasConfig ? Path.Combine(folder, "Config") : folder);
    return folder;
  }

  private string Install(string name, string? variant = null)
  {
    var folder = Path.Combine(_directory, name);
    Directory.CreateDirectory(folder);
    if (variant is not null) File.WriteAllText(Path.Combine(folder, "dcs_variant.txt"), variant);
    return folder;
  }

  [Fact]
  public async Task UsesTheSavedGamesFolderOfEachInstall()
  {
    var dcs = WriteFolder("DCS");
    var openBeta = WriteFolder("DCS.openbeta");
    WriteFolder("DCS.old");

    var folders = await DcsFolders.FindAsync(SavedGames, [Install("DCS World"), Install("DCS World OpenBeta", "openbeta\r\n")]);

    Assert.Equal([dcs, openBeta], folders);
  }

  [Fact]
  public async Task FindsTheDcsFoldersWhenNoInstallHasRunYet()
  {
    var dcs = WriteFolder("DCS");
    var openBeta = WriteFolder("DCS.openbeta");
    WriteFolder("DCS_F4E");
    WriteFolder("DCS_OH58D");
    WriteFolder("DCS.empty", hasConfig: false);

    var folders = await DcsFolders.FindAsync(SavedGames, [Install("DCS World", "release_server")]);

    Assert.Equal([dcs, openBeta], folders);
  }

  [Fact]
  public async Task FindsNothingWithoutDcs()
  {
    Assert.Empty(await DcsFolders.FindAsync(SavedGames, []));
  }
}
