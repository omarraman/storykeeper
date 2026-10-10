if (args.Length > 1)
{
    Console.Error.WriteLine("Usage: StoryKeeper.SourceArchiver [source-directory]");
    return 2;
}

var sourceDirectory = Path.GetFullPath(
    args.Length > 0 ? args[0] : Path.Combine(Directory.GetCurrentDirectory(), "src"));
var documentationDirectory = Path.GetFullPath(
    Path.Combine(Directory.GetCurrentDirectory(), "docs"));
var clientSourceDirectory = Path.GetFullPath(
    Path.Combine(Directory.GetCurrentDirectory(), "client", "src"));

if (!Directory.Exists(sourceDirectory))
{
    Console.Error.WriteLine($"Source directory does not exist: {sourceDirectory}");
    return 1;
}

if (args.Length == 0 && !Directory.Exists(documentationDirectory))
{
    Console.Error.WriteLine($"Documentation directory does not exist: {documentationDirectory}");
    return 1;
}

var includeClientSource = Directory.Exists(clientSourceDirectory);

if (args.Length == 0 && !includeClientSource)
{
    Console.Error.WriteLine($"Client source directory does not exist: {clientSourceDirectory}");
    return 1;
}

var documentsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
if (string.IsNullOrWhiteSpace(documentsDirectory))
{
    Console.Error.WriteLine("Could not determine the user's Documents directory.");
    return 1;
}

var archiveDirectory = Path.Combine(documentsDirectory, "codearchives");
var destinationDirectory = Path.Combine(
    archiveDirectory,
    $"{new DirectoryInfo(sourceDirectory).Name}-{DateTime.Now:yyyyMMdd-HHmmss}");

Directory.CreateDirectory(destinationDirectory);

var sourceFileCount = CopyDirectory(sourceDirectory, destinationDirectory);
var documentationFileCount = args.Length == 0
    ? CopyDirectory(documentationDirectory, Path.Combine(destinationDirectory, "docs"))
    : 0;
var clientFileCount = includeClientSource
    ? CopyDirectory(
        clientSourceDirectory,
        Path.Combine(destinationDirectory, "client", "src"),
        IsIncludedClientFile)
    : 0;

Console.WriteLine(
    $"Copied {sourceDirectory}" +
    $"{(args.Length == 0 ? $" and {documentationDirectory}" : string.Empty)}" +
    $"{(includeClientSource ? $" and {clientSourceDirectory}" : string.Empty)} " +
    $"to {destinationDirectory} with {sourceFileCount + documentationFileCount + clientFileCount} file(s).");
return 0;

static int CopyDirectory(
    string sourceDirectory,
    string destinationDirectory,
    Func<string, bool>? fileFilter = null)
{
    var sourceFiles = Directory
        .EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories)
        .Where(file =>
            !HasExcludedDirectory(file, sourceDirectory) &&
            (fileFilter is null ? IsIncludedFile(file) : fileFilter(file)))
        .ToArray();

    foreach (var sourceFile in sourceFiles)
    {
        var relativePath = Path.GetRelativePath(sourceDirectory, sourceFile);
        var destinationFile = Path.Combine(destinationDirectory, relativePath + ".txt");
        var destinationFileDirectory = Path.GetDirectoryName(destinationFile);

        if (destinationFileDirectory is not null)
            Directory.CreateDirectory(destinationFileDirectory);

        File.Copy(sourceFile, destinationFile, overwrite: true);
    }

    return sourceFiles.Length;
}

static bool IsIncludedClientFile(string filePath)
{
    var extension = Path.GetExtension(filePath);

    return extension.Equals(".ts", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".tsx", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".css", StringComparison.OrdinalIgnoreCase);
}

static bool IsIncludedFile(string filePath)
{
    var extension = Path.GetExtension(filePath);

    return extension.Equals(".cs", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".md", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".prj", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".sln", StringComparison.OrdinalIgnoreCase);
}

static bool HasExcludedDirectory(string filePath, string sourceDirectory)
{
    var relativePath = Path.GetRelativePath(sourceDirectory, filePath);
    var directorySegments = relativePath
        .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    return directorySegments
        .Take(directorySegments.Length - 1)
        .Any(segment =>
            segment.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("debug", StringComparison.OrdinalIgnoreCase));
}
