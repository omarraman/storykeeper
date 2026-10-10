using System.IO.Compression;

namespace Storykeeper.Archiver;

public static class SolutionArchiver
{
    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        ".idea",
        ".vs",
        "bin",
        "coverage",
        "dist",
        "node_modules",
        "obj",
        "tts-cache"
    };
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static async Task<int> CreateArchiveAsync(
        string sourceDirectory,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var sourceFullPath = Path.GetFullPath(sourceDirectory);
        if (!Directory.Exists(sourceFullPath))
        {
            throw new DirectoryNotFoundException($"Source directory does not exist: {sourceFullPath}");
        }

        var outputFullPath = Path.GetFullPath(outputPath);
        var entries = CollectEntries(sourceFullPath, outputFullPath);
        var outputDirectory = Path.GetDirectoryName(outputFullPath);
        if (outputDirectory is not null)
        {
            Directory.CreateDirectory(outputDirectory);
        }

        await using var outputStream = new FileStream(
            outputFullPath,
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);
        using var archive = new ZipArchive(outputStream, ZipArchiveMode.Create, leaveOpen: true);

        var fileCount = 0;
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.IsDirectory)
            {
                archive.CreateEntry(entry.ArchivePath);
                continue;
            }

            var archiveEntry = archive.CreateEntry(entry.ArchivePath, CompressionLevel.Optimal);
            await using var sourceStream = new FileStream(
                entry.FullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                useAsync: true);
            await using var entryStream = archiveEntry.Open();
            await sourceStream.CopyToAsync(entryStream, cancellationToken);
            fileCount++;
        }

        return fileCount;
    }

    private static List<ArchiveEntryInfo> CollectEntries(string sourceDirectory, string outputPath)
    {
        var entries = new List<ArchiveEntryInfo>();
        var directories = new Stack<string>();
        directories.Push(sourceDirectory);

        while (directories.TryPop(out var currentDirectory))
        {
            foreach (var directory in Directory.EnumerateDirectories(currentDirectory)
                         .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                var directoryInfo = new DirectoryInfo(directory);
                if (ExcludedDirectories.Contains(directoryInfo.Name)
                    || IsReparsePoint(directoryInfo.Attributes))
                {
                    continue;
                }

                var relativePath = Path.GetRelativePath(sourceDirectory, directory)
                    .Replace(Path.DirectorySeparatorChar, '/')
                    .TrimEnd('/');
                entries.Add(new ArchiveEntryInfo(directory, $"{relativePath}/", IsDirectory: true));
                directories.Push(directory);
            }

            foreach (var file in Directory.EnumerateFiles(currentDirectory)
                         .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                var fileInfo = new FileInfo(file);
                if (IsReparsePoint(fileInfo.Attributes)
                    || string.Equals(Path.GetFullPath(file), outputPath, PathComparison)
                    || ShouldExcludeFile(fileInfo.Name))
                {
                    continue;
                }

                var relativePath = Path.GetRelativePath(sourceDirectory, file)
                    .Replace(Path.DirectorySeparatorChar, '/');
                entries.Add(new ArchiveEntryInfo(file, relativePath, IsDirectory: false));
            }
        }

        return entries
            .OrderBy(entry => entry.ArchivePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool ShouldExcludeFile(string fileName)
    {
        if (fileName.Equals(".env", StringComparison.OrdinalIgnoreCase)
            || (fileName.StartsWith(".env.", StringComparison.OrdinalIgnoreCase)
                && !fileName.Equals(".env.example", StringComparison.OrdinalIgnoreCase))
            || (fileName.StartsWith("appsettings.", StringComparison.OrdinalIgnoreCase)
                && fileName.EndsWith(".local.json", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var extension = Path.GetExtension(fileName);
        if (extension.Equals(".dll", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".pdb", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".so", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".dylib", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return fileName.Contains(".db", StringComparison.OrdinalIgnoreCase)
            && (fileName.EndsWith(".db", StringComparison.OrdinalIgnoreCase)
                || fileName.Contains(".db-", StringComparison.OrdinalIgnoreCase)
                || fileName.Contains(".db.", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsReparsePoint(FileAttributes attributes) =>
        (attributes & FileAttributes.ReparsePoint) != 0;

    private sealed record ArchiveEntryInfo(string FullPath, string ArchivePath, bool IsDirectory);
}
