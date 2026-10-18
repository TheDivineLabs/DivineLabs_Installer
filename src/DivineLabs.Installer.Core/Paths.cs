using System.Text.RegularExpressions;
namespace DivineLabs.Installer.Core;

public static partial class Paths {
    public const string StateDirectory = ".divinelabs-installer";
    public const string ReceiptPath = StateDirectory + "/receipt.json";
    public const string RuntimeDirectory = StateDirectory + "/runtime";
    public static readonly string[] SupportedRuntimes = ["win-x64", "win-arm64", "osx-x64", "osx-arm64", "linux-x64", "linux-arm64"];
    [GeneratedRegex(@"^[a-zA-Z0-9][a-zA-Z0-9.-]{2,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex Identifier();
    public static string Relative(string value) {
        if (string.IsNullOrWhiteSpace(value) || value.Contains('\\') || value.Contains(':') || Path.IsPathRooted(value) ||
            value.Any(c => char.IsControl(c) || "<>\"|?*".Contains(c))) throw new InvalidDataException($"Invalid package path: {value}");
        foreach (string part in value.Split('/')) {
            string stem = part.Split('.')[0].ToUpperInvariant();
            if (part is "" or "." or ".." || part.EndsWith('.') || part.EndsWith(' ') ||
                stem is "CON" or "PRN" or "AUX" or "NUL" ||
                (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && stem[3] is >= '0' and <= '9'))
                throw new InvalidDataException($"Non-portable package path: {value}");
        }
        return value;
    }
    public static string Inside(string root, string relative) {
        Relative(relative);
        string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string result = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!result.StartsWith(fullRoot, StringComparison.Ordinal)) throw new InvalidDataException("Path escapes installation directory.");
        return result;
    }
    public static void NoLinks(string path) {
        for (string? current = Path.GetFullPath(path); current != null; current = Path.GetDirectoryName(current)) {
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException($"Symbolic links and reparse points are not supported: {current}");
            // Dangling symbolic links are reported by LinkTarget even when Exists is false.
            if (new FileInfo(current).LinkTarget != null) throw new IOException($"Linked path: {current}");
        }
    }
    public static string Target(string root) {
        string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        NoLinks(full);
        if (Path.GetDirectoryName(full) == null || full == Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) ||
            (OperatingSystem.IsWindows() && full.Equals(Environment.GetFolderPath(Environment.SpecialFolder.Windows), StringComparison.OrdinalIgnoreCase)) ||
            Directory.Exists(Path.Combine(full, ".git"))) throw new IOException("Choose a dedicated application directory.");
        return full;
    }
    public static void Validate(ProductConfig config) {
        if (config.SchemaVersion != 1 || !Identifier().IsMatch(config.Id)) throw new InvalidDataException("schemaVersion must be 1 and id must be a stable reverse-domain identifier.");
        foreach (string label in new[] { config.Name, config.Publisher, config.Version })
            if (string.IsNullOrWhiteSpace(label) || label.Any(char.IsControl)) throw new InvalidDataException("name, publisher and version are required, single-line values.");
        Relative(config.InstallDirectoryName.Length == 0 ? config.Name : config.InstallDirectoryName);
        if ((config.InstallDirectoryName.Length == 0 ? config.Name : config.InstallDirectoryName).Contains('/')) throw new InvalidDataException("installDirectoryName must be one directory name.");
        if (config.Publisher.Contains('/') || config.Publisher.Contains('\\')) throw new InvalidDataException("publisher must be one directory name.");
        Relative(config.Publisher);
        if (config.Language is not ("en" or "ru")) throw new InvalidDataException("Supported languages: en, ru.");
        if (!Regex.IsMatch(config.Branding.Accent, "^#[0-9a-fA-F]{6}$")) throw new InvalidDataException("branding.accent must be #RRGGBB.");
        foreach (var link in config.Links) if (string.IsNullOrWhiteSpace(link.Label) || link.Label.Any(char.IsControl) || !Uri.TryCreate(link.Url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) throw new InvalidDataException("Links require labels and HTTP(S) URLs.");
        foreach (string data in config.DataDirectories) {
            Relative(data);
            if (data.Split('/')[0].Equals(StateDirectory, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Reserved data directory.");
        }
    }
    public static bool IsData(ProductConfig product, string path) => product.DataDirectories.Any(d =>
        path.Equals(d, StringComparison.OrdinalIgnoreCase) || path.StartsWith(d + "/", StringComparison.OrdinalIgnoreCase));
    public static void DeleteOwnedTree(string root) {
        NoLinks(root);
        if (!Directory.Exists(root)) return;
        foreach (string item in Package.EnumerateFiles(root)) NoLinks(item);
        Directory.Delete(root, true);
    }
}
