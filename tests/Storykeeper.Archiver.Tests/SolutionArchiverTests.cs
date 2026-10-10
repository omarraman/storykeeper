using System.IO.Compression;
using Storykeeper.Archiver;
using Xunit;

namespace Storykeeper.Archiver.Tests;

public sealed class SolutionArchiverTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task CreateArchiveAsync_IncludesSourceAndDocumentationButSkipsGeneratedAndSecretFiles()
    {
        Directory.CreateDirectory(_root);
        WriteFile("Storykeeper.slnx");
        WriteFile("src/Storykeeper.Api/Storykeeper.Api.csproj");
        WriteFile("src/Storykeeper.Api/Program.cs");
        WriteFile("docs/architecture.md");
        WriteFile(".github/workflows/build.yml");
        WriteFile(".env.example");
        WriteFile("src/Storykeeper.Api/bin/Debug/net10.0/Storykeeper.Api.dll");
        WriteFile("src/Storykeeper.Api/obj/project.assets.json");
        WriteFile("src/Storykeeper.Api/Generated.dll");
        WriteFile("node_modules/package/index.js");
        WriteFile(".env");
        WriteFile("storykeeper.db");
        WriteFile("appsettings.Development.local.json");
        var archivePath = Path.Combine(_root, "output", "context.zip");

        var count = await SolutionArchiver.CreateArchiveAsync(_root, archivePath);

        using var archive = ZipFile.OpenRead(archivePath);
        var entryNames = archive.Entries.Select(entry => entry.FullName).ToHashSet();
        Assert.Equal(6, count);
        Assert.Contains("Storykeeper.slnx", entryNames);
        Assert.Contains("src/Storykeeper.Api/Storykeeper.Api.csproj", entryNames);
        Assert.Contains("src/Storykeeper.Api/Program.cs", entryNames);
        Assert.Contains("docs/architecture.md", entryNames);
        Assert.Contains(".github/workflows/build.yml", entryNames);
        Assert.Contains(".env.example", entryNames);
        Assert.Contains("src/", entryNames);
        Assert.DoesNotContain(entryNames, name =>
            name.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            || name.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
            || name.Contains("node_modules", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(entryNames, name =>
            name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            || name.Equals(".env", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".db", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".local.json", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CreateArchiveAsync_DoesNotIncludeItsOutputWhenInsideSource()
    {
        Directory.CreateDirectory(_root);
        WriteFile("README.md");
        var archivePath = Path.Combine(_root, "context.zip");

        await SolutionArchiver.CreateArchiveAsync(_root, archivePath);

        using var archive = ZipFile.OpenRead(archivePath);
        Assert.DoesNotContain(archive.Entries, entry => entry.FullName == "context.zip");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private void WriteFile(string relativePath)
    {
        var path = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "sample");
    }
}
