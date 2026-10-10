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

var sourceDirectory = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
var sourceFullPath = Path.GetFullPath(sourceDirectory);
var outputPath = args.Length > 1
    ? args[1]
    : Path.Combine(
        Directory.GetParent(sourceFullPath)?.FullName ?? sourceFullPath,
        "Storykeeper-Perplexity.zip");

try
{
    var fileCount = await SolutionArchiver.CreateArchiveAsync(sourceFullPath, outputPath);
    Console.WriteLine($"Created {Path.GetFullPath(outputPath)} with {fileCount} files.");
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
        "Usage: Storykeeper.Archiver [source-directory] [output.zip]\n" +
        "Defaults to the current directory and creates the ZIP beside it.");
}
