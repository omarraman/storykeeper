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
        string archiveDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(archiveDirectory);

        var sourceFullPath = Path.GetFullPath(sourceDirectory);
        if (!Directory.Exists(sourceFullPath))
        {
            throw new DirectoryNotFoundException($"Source directory does not exist: {sourceFullPath}");
        }

        var archiveFullPath = Path.GetFullPath(archiveDirectory);
        if (Directory.Exists(archiveFullPath) || File.Exists(archiveFullPath))
        {
            throw new IOException($"Archive directory already exists: {archiveFullPath}");
        }

        var entries = CollectEntries(sourceFullPath, archiveFullPath);
        Directory.CreateDirectory(archiveFullPath);

        var fileCount = 0;
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.IsDirectory)
            {
                Directory.CreateDirectory(Path.Combine(archiveFullPath, entry.RelativePath));
                continue;
            }

            var destinationPath = Path.Combine(archiveFullPath, entry.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            await using var sourceStream = new FileStream(
                entry.FullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                useAsync: true);
            await using var destinationStream = new FileStream(
                destinationPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true);
            await sourceStream.CopyToAsync(destinationStream, cancellationToken);
            fileCount++;
        }

        return fileCount;
    }

    private static List<ArchiveEntryInfo> CollectEntries(string sourceDirectory, string archiveDirectory)
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
                    || IsReparsePoint(directoryInfo.Attributes)
                    || IsSamePath(directory, archiveDirectory))
                {
                    continue;
                }

                var relativePath = Path.GetRelativePath(sourceDirectory, directory)
                    .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
                entries.Add(new ArchiveEntryInfo(directory, relativePath, IsDirectory: true));
                directories.Push(directory);
            }

            foreach (var file in Directory.EnumerateFiles(currentDirectory)
                         .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                var fileInfo = new FileInfo(file);
                if (IsReparsePoint(fileInfo.Attributes)
                    || IsSamePath(file, archiveDirectory)
                    || ShouldExcludeFile(fileInfo.Name))
                {
                    continue;
                }

                var relativePath = Path.GetRelativePath(sourceDirectory, file)
                    .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
                entries.Add(new ArchiveEntryInfo(file, relativePath, IsDirectory: false));
            }
        }

        return entries
            .OrderBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsSamePath(string path, string otherPath) =>
        string.Equals(Path.GetFullPath(path), otherPath, PathComparison);

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

    private sealed record ArchiveEntryInfo(string FullPath, string RelativePath, bool IsDirectory);
}
