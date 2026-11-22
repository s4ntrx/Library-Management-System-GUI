using LibraryManagement.Exceptions;
using LibraryManagement.Services;
using LibraryManagement.Storage;

if (args.Contains("--help") || args.Contains("-h"))
{
    Console.WriteLine("""
        Usage: library [--data <file>]

        Options:
          --data <file>   JSON file that stores the library (default: see below)
          -h, --help      Show this help

        Data file lookup order:
          1. --data <file>
          2. environment variable LIBRARY_DATA_FILE
          3. ~/.local/share/library-management/library.json (Linux)

        Exit codes: 0 success, 1 unexpected error, 2 data file error, 64 bad usage.
        """);
    return 0;
}

var dataIndex = Array.IndexOf(args, "--data");
if (dataIndex >= 0 && dataIndex + 1 >= args.Length)
{
    Console.Error.WriteLine("--data needs a file path. Run with --help for usage.");
    return 64;
}

var dataPath = LibraryPaths.Resolve(dataIndex >= 0 ? args[dataIndex + 1] : null);

// Ctrl+C ends the process immediately. Nothing is lost because every change is saved as it happens.
Console.CancelKeyPress += (_, _) =>
    Console.WriteLine($"{Environment.NewLine}Interrupted. All completed changes are already saved.");

try
{
    var library = new LibraryService(new JsonFileLibraryStore(dataPath));
    Console.WriteLine($"Library Management System. Data file: {dataPath}");
    new ConsoleMenu(library, Console.In, Console.Out).Run();
    return 0;
}
catch (StorageException ex)
{
    Console.Error.WriteLine($"Storage error: {ex.Message}");
    return 2;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Unexpected error: {ex.Message}");
    return 1;
}
