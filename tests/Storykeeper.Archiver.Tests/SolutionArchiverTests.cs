using Storykeeper.Archiver;
using Xunit;

namespace Storykeeper.Archiver.Tests;

public sealed class SolutionArchiverTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task CreateArchiveAsync_CopiesSourceAndDocumentationButSkipsGeneratedAndSecretFiles()
    {
        Directory.CreateDirectory(_root);
        WriteFile("Storykeeper.slnx");
        WriteFile("src/Storykeeper.Api/Storykeeper.Api.csproj");
        WriteFile("src/Storykeeper.Api/Program.cs");
        WriteFile("docs/architecture.md");
        Directory.CreateDirectory(Path.Combine(_root, "docs", "empty-topic"));
        WriteFile(".github/workflows/build.yml");
        WriteFile(".env.example");
        WriteFile("src/Storykeeper.Api/bin/Debug/net10.0/Storykeeper.Api.dll");
        WriteFile("src/Storykeeper.Api/obj/project.assets.json");
        WriteFile("src/Storykeeper.Api/Generated.dll");
        WriteFile("node_modules/package/index.js");
        WriteFile(".env");
        WriteFile("storykeeper.db");
        WriteFile("appsettings.Development.local.json");
        var archivePath = Path.Combine(_root, "output", "20261010-0750");

        var count = await SolutionArchiver.CreateArchiveAsync(_root, archivePath);

        var entryNames = Directory.GetFiles(archivePath, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(archivePath, path)
                .Replace(Path.DirectorySeparatorChar, '/'))
            .ToHashSet();
        Assert.Equal(6, count);
        Assert.Contains("Storykeeper.slnx", entryNames);
        Assert.Contains("src/Storykeeper.Api/Storykeeper.Api.csproj", entryNames);
        Assert.Contains("src/Storykeeper.Api/Program.cs", entryNames);
        Assert.Contains("docs/architecture.md", entryNames);
        Assert.Contains(".github/workflows/build.yml", entryNames);
        Assert.Contains(".env.example", entryNames);
        Assert.True(Directory.Exists(Path.Combine(archivePath, "docs", "empty-topic")));
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
    public async Task CreateArchiveAsync_DoesNotIncludeItsDestinationWhenInsideSource()
    {
        Directory.CreateDirectory(_root);
        WriteFile("README.md");
        WriteFile("source/README.md");
        var sourcePath = Path.Combine(_root, "source");
        var archivePath = Path.Combine(sourcePath, "archives", "dated-copy");

        var count = await SolutionArchiver.CreateArchiveAsync(sourcePath, archivePath);

        Assert.Equal(1, count);
        Assert.True(File.Exists(Path.Combine(archivePath, "README.md")));
        Assert.False(Directory.Exists(Path.Combine(archivePath, "archives")));
    }

    [Fact]
    public async Task CreateArchiveAsync_RejectsExistingDestination()
    {
        Directory.CreateDirectory(_root);
        WriteFile("README.md");
        var archivePath = Path.Combine(_root, "existing");
        Directory.CreateDirectory(archivePath);

        await Assert.ThrowsAsync<IOException>(() =>
            SolutionArchiver.CreateArchiveAsync(_root, archivePath));
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
