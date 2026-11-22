namespace LibraryManagement.Storage;

/// <summary>Decides which data file the console and web interfaces share.</summary>
public static class LibraryPaths
{
    public const string DataFileEnvironmentVariable = "LIBRARY_DATA_FILE";

    public static string Resolve(string? explicitPath = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
            return explicitPath;

        var fromEnvironment = Environment.GetEnvironmentVariable(DataFileEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
            return fromEnvironment;

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "library-management",
            "library.json");
    }
}
