using System.Globalization;
using Storykeeper.Archiver;

if (args.Length == 1 && args[0] is "--help" or "-h")
{
    PrintUsage();
    return 0;
}

if (args.Length > 2)
{
    PrintUsage();
    return 2;
}

try
{
    var sourceDirectory = args.Length > 0
        ? args[0]
        : FindSolutionDirectory(Directory.GetCurrentDirectory(), AppContext.BaseDirectory);
    var archiveRoot = args.Length > 1
        ? Path.GetFullPath(args[1])
        : Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "codearchives",
            "StoryKeeper");
    var archiveDirectory = Path.Combine(
        archiveRoot,
        DateTime.Now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture));
    var fileCount = await SolutionArchiver.CreateArchiveAsync(sourceDirectory, archiveDirectory);
    Console.WriteLine($"Copied {fileCount} files to {archiveDirectory}");
    return 0;
}
catch (Exception exception) when (exception is IOException
    or UnauthorizedAccessException
    or ArgumentException
    or NotSupportedException)
{
    Console.Error.WriteLine($"Unable to create archive: {exception.Message}");
    return 1;
}

static void PrintUsage()
{
    Console.WriteLine(
        "Usage: Storykeeper.Archiver [source-directory] [archive-root]\n" +
        "Defaults to the Storykeeper solution containing the current directory and " +
        "creates a dated folder under Documents\\codearchives\\StoryKeeper.");
}

static string FindSolutionDirectory(params string[] startingDirectories)
{
    foreach (var startingDirectory in startingDirectories)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(startingDirectory));
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Storykeeper.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }
    }

    throw new DirectoryNotFoundException(
        "Could not find Storykeeper.slnx. Pass the solution directory as the first argument.");
}
