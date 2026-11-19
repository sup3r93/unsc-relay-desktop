namespace UnscRelay.Core;

/// <summary>Opt-in smoke fixtures are existing direct children of the checked temporary directory.</summary>
public static class SmokeFixturePaths
{
    public static string Root(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathFullyQualified(directory) || directory.Any(char.IsControl) || directory.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(part => part is "." or ".."))
            throw new IOException("Fixture directory must be absolute.");
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(directory));
        if (!name.StartsWith("relay-ssh-", StringComparison.Ordinal) || name.Length > 100)
            throw new IOException("Unexpected fixture directory name.");
        var expected = LocalDataFiles.Child(Path.GetTempPath(), name);
        if (!string.Equals(Path.GetFullPath(directory), expected, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(expected))
            throw new IOException("Fixture must be an existing direct child of the temporary directory.");
        return expected;
    }
}
