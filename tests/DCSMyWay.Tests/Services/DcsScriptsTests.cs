using System.Text;
using DCSMyWay.Services;

namespace DCSMyWay.Tests.Services;

public sealed class DcsScriptsTests : IDisposable
{
  private const string OtherExport = "local wwtlfs = require('lfs')\r\ndofile(wwtlfs.writedir() .. 'Scripts/wwt/wwtExport.lua')\r\n";
  private const string AllowingAutoexec = """
    if not net then net = {} end

    net.allow_unsafe_api = {  -- this defines the secure zones where net.dostring_in() can be called from
      "userhooks",
      "scripting",
    }

    net.allow_dostring_in = {
      "mission",
      "gui",
    }
    """;

  private readonly string _directory = Path.Combine(Path.GetTempPath(), $"DCSMyWay-tests-{Guid.NewGuid()}");
  private readonly DcsScripts _scripts;

  public DcsScriptsTests()
  {
    _scripts = new DcsScripts(Source);
    Write(Path.Combine(Source, "Scripts", "Hooks", "DCSMyWay-hook.lua"), "hook");
    Write(Path.Combine(Source, "Scripts", "DCSMyWay", "DCSMyWayExport.lua"), "export");
    Write(Path.Combine(Source, "Scripts", "DCSMyWay", "Images", "Crosshair.png"), "png");
    Directory.CreateDirectory(Path.Combine(WriteFolder, "Config"));
  }

  private string Source => Path.Combine(_directory, "app", "DCS");

  private string WriteFolder => Path.Combine(_directory, "Saved Games", "DCS");

  private string ExportLua => Path.Combine(WriteFolder, DcsScripts.ExportLua);

  private string Autoexec => Path.Combine(WriteFolder, DcsScripts.Autoexec);

  public void Dispose()
  {
    if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
  }

  private static void Write(string path, string text)
  {
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllBytes(path, Encoding.Latin1.GetBytes(text));
  }

  private static string Read(string path) => Encoding.Latin1.GetString(File.ReadAllBytes(path));

  private string[] Backups(string path) => Directory.GetFiles(Path.GetDirectoryName(path)!, $"{Path.GetFileName(path)}.bak-dcsmyway-*");

  [Fact]
  public async Task InstallsTheScriptsTheExportLineAndMapDrawingTheFirstTime()
  {
    var changes = await _scripts.InstallAsync(WriteFolder);

    Assert.Equal("hook", Read(Path.Combine(WriteFolder, "Scripts", "Hooks", "DCSMyWay-hook.lua")));
    Assert.Equal("png", Read(Path.Combine(WriteFolder, "Scripts", "DCSMyWay", "Images", "Crosshair.png")));
    Assert.Equal($"{DcsScripts.ExportLine}\r\n", Read(ExportLua));
    Assert.Equal(
      "if not net then net = {} end\r\n"
      + "net.allow_unsafe_api = net.allow_unsafe_api or {}\r\n"
      + "net.allow_unsafe_api[#net.allow_unsafe_api + 1] = \"userhooks\"\r\n"
      + "net.allow_dostring_in = net.allow_dostring_in or {}\r\n"
      + "net.allow_dostring_in[#net.allow_dostring_in + 1] = \"mission\"\r\n",
      Read(Autoexec));
    Assert.Equal(5, changes.Count);
    Assert.Contains(DcsScripts.ExportLua, changes);
    Assert.Contains(DcsScripts.Autoexec, changes);
    Assert.True(DcsScripts.NeedsDcsRestart(changes));
  }

  [Fact]
  public async Task ChangesNothingWhenEverythingIsInstalled()
  {
    await _scripts.InstallAsync(WriteFolder);

    Assert.Empty(await _scripts.InstallAsync(WriteFolder));
    Assert.Empty(Backups(ExportLua));
    Assert.Empty(Backups(Autoexec));
  }

  [Fact]
  public async Task UpdatesOnlyTheScriptsThatChanged()
  {
    await _scripts.InstallAsync(WriteFolder);
    Write(Path.Combine(Source, "Scripts", "DCSMyWay", "DCSMyWayExport.lua"), "export 2");

    var changes = await _scripts.InstallAsync(WriteFolder);

    Assert.Equal([DcsScripts.ExportScript], changes);
    Assert.False(DcsScripts.NeedsDcsRestart(changes));
    Assert.Equal("export 2", Read(Path.Combine(WriteFolder, DcsScripts.ExportScript)));
  }

  [Fact]
  public async Task AsksToRestartDcsWhenTheHookChanges()
  {
    await _scripts.InstallAsync(WriteFolder);
    Write(Path.Combine(Source, "Scripts", "Hooks", "DCSMyWay-hook.lua"), "hook 2");

    Assert.True(DcsScripts.NeedsDcsRestart(await _scripts.InstallAsync(WriteFolder)));
  }

  [Fact]
  public async Task AppendsTheExportLineKeepingEveryOtherByteAndBacksUpTheFile()
  {
    var original = OtherExport + "local SimShakerlfs=require('lfs'); dofile(SimShakerlfs.writedir()..'Scripts/SimShaker.lua')";
    Write(ExportLua, original);

    await _scripts.InstallAsync(WriteFolder);

    Assert.Equal($"{original}\r\n{DcsScripts.ExportLine}\r\n", Read(ExportLua));
    Assert.Equal(original, Read(Assert.Single(Backups(ExportLua))));
  }

  [Fact]
  public async Task KeepsLfLineEndings()
  {
    Write(ExportLua, "dofile('a.lua')\n");

    await _scripts.InstallAsync(WriteFolder);

    Assert.Equal($"dofile('a.lua')\n{DcsScripts.ExportLine}\n", Read(ExportLua));
  }

  [Fact]
  public async Task KeepsAnExportLineAlreadyThereInAnyForm()
  {
    var original = OtherExport + "pcall(function() local lfs=require('lfs'); dofile(lfs.writedir()..[[Scripts\\DCSMyWay\\DCSMyWayExport.lua]]) end)\r\n";
    Write(ExportLua, original);

    var changes = await _scripts.InstallAsync(WriteFolder);

    Assert.DoesNotContain(DcsScripts.ExportLua, changes);
    Assert.Equal(original, Read(ExportLua));
    Assert.Empty(Backups(ExportLua));
  }

  [Fact]
  public async Task AddsTheExportLineWhenItIsCommentedOut()
  {
    Write(ExportLua, $"-- {DcsScripts.ExportLine}\r\n");

    await _scripts.InstallAsync(WriteFolder);

    Assert.Equal($"-- {DcsScripts.ExportLine}\r\n{DcsScripts.ExportLine}\r\n", Read(ExportLua));
  }

  [Fact]
  public async Task LeavesAnAutoexecThatAlreadyAllowsMapDrawing()
  {
    Write(Autoexec, AllowingAutoexec);

    var changes = await _scripts.InstallAsync(WriteFolder);

    Assert.DoesNotContain(DcsScripts.Autoexec, changes);
    Assert.Equal(AllowingAutoexec, Read(Autoexec));
  }

  [Fact]
  public async Task AllowsMapDrawingWhenTheAutoexecOnlyAllowsOtherStates()
  {
    var original = "net.allow_unsafe_api = {\n  -- \"userhooks\",\n  \"gui\",\n}\nnet.allow_dostring_in = { \"mission\" }\n";
    Write(Autoexec, original);

    var changes = await _scripts.InstallAsync(WriteFolder);

    Assert.Contains(DcsScripts.Autoexec, changes);
    Assert.StartsWith(original + "if not net then net = {} end\n", Read(Autoexec));
    Assert.EndsWith("= \"mission\"\n", Read(Autoexec));
    Assert.Equal(original, Read(Assert.Single(Backups(Autoexec))));
  }

  [Fact]
  public async Task RemovesOnlyWhatItInstalled()
  {
    var otherHook = Path.Combine(WriteFolder, "Scripts", "Hooks", "bhHook.lua");
    Write(otherHook, "other hook");
    Write(ExportLua, OtherExport);
    Write(Autoexec, "options = {}\r\n");
    await _scripts.InstallAsync(WriteFolder);

    await _scripts.RemoveAsync(WriteFolder);

    Assert.Equal(OtherExport, Read(ExportLua));
    Assert.Equal("options = {}\r\n", Read(Autoexec));
    Assert.Equal("other hook", Read(otherHook));
    Assert.False(File.Exists(Path.Combine(WriteFolder, "Scripts", "Hooks", "DCSMyWay-hook.lua")));
    Assert.False(Directory.Exists(Path.Combine(WriteFolder, "Scripts", "DCSMyWay")));
  }

  [Fact]
  public async Task RemovesTheFilesItCreated()
  {
    await _scripts.InstallAsync(WriteFolder);

    await _scripts.RemoveAsync(WriteFolder);

    Assert.False(File.Exists(ExportLua));
    Assert.False(File.Exists(Autoexec));
    Assert.False(Directory.Exists(Path.Combine(WriteFolder, "Scripts")));
    Assert.True(Directory.Exists(Path.Combine(WriteFolder, "Config")));
  }

  [Fact]
  public void ShipsTheScriptsTheExportLineLoads()
  {
    Assert.Contains("'Scripts/DCSMyWay/DCSMyWayExport.lua'", DcsScripts.ExportLine);
    Assert.True(File.Exists(Path.Combine(DcsScripts.DefaultSource, DcsScripts.ExportScript)));
    Assert.True(File.Exists(Path.Combine(DcsScripts.DefaultSource, "Scripts", "Hooks", "DCSMyWay-hook.lua")));
  }
}
