using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
namespace DivineLabs.Installer.Core;

public sealed class Package : IDisposable {
    public const string FileName = "application.dlpack";
    private readonly ZipArchive zip;
    public PackageManifest Manifest { get; }
    public Package(string path) {
        zip = ZipFile.OpenRead(path);
        try {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in zip.Entries) {
                Paths.Relative(entry.FullName);
                if (!names.Add(entry.FullName) || ((entry.ExternalAttributes >> 16) & 0xf000) == 0xa000)
                    throw new InvalidDataException("Duplicate or symbolic-link archive entry.");
            }
            using var stream = (zip.GetEntry("manifest.json") ?? throw new InvalidDataException("Package has no manifest.")).Open();
            Manifest = JsonSerializer.Deserialize(stream, InstallerJson.Default.PackageManifest) ?? throw new InvalidDataException("Empty manifest.");
            Validate();
        } catch { zip.Dispose(); throw; }
    }
    private void Validate() {
        if (Manifest.FormatVersion != 1 || !Paths.SupportedRuntimes.Contains(Manifest.Runtime)) throw new InvalidDataException("Unsupported package format/runtime.");
        Paths.Validate(Manifest.Product); Paths.Relative(Manifest.EntryPoint);
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entries = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "manifest.json" };
        foreach (var item in Manifest.Files.Concat(Manifest.Assets)) {
            Paths.Relative(item.Path);
            bool asset = Manifest.Assets.Contains(item);
            if (!files.Add((asset ? "asset/" : "file/") + item.Path) || item.Length < 0 ||
                item.Sha256.Length != 64 || !item.Sha256.All(Uri.IsHexDigit)) throw new InvalidDataException("Invalid file inventory.");
            if (!asset && (item.Path.Split('/')[0].Equals(Paths.StateDirectory, StringComparison.OrdinalIgnoreCase) || Paths.IsData(Manifest.Product, item.Path)))
                throw new InvalidDataException("Payload overlaps managed state or preserved user data.");
            string entryName = (asset ? "assets/" : "payload/") + item.Path;
            var entry = zip.GetEntry(entryName) ?? throw new InvalidDataException($"Missing file: {item.Path}");
            if (entry.Length != item.Length) throw new InvalidDataException("File size mismatch.");
            entries.Add(entryName);
        }
        if (!Manifest.Files.Any(f => f.Path == Manifest.EntryPoint) || zip.Entries.Any(e => !entries.Contains(e.FullName)))
            throw new InvalidDataException("Missing entry point or unlisted package content.");
        foreach (string path in new[] { Manifest.Product.Branding.Icon, Manifest.Product.Branding.Cover, Manifest.Product.License }.OfType<string>())
            if (!Manifest.Assets.Any(a => a.Path == path)) throw new InvalidDataException("Unlisted branding/license asset.");
    }
    public byte[]? ReadAsset(string? path) {
        if (path == null) return null;
        var file = Manifest.Assets.Single(a => a.Path == path);
        if (file.Length > 16 * 1024 * 1024) throw new InvalidDataException("Branding/license exceeds 16 MiB.");
        using var input = zip.GetEntry("assets/" + path)!.Open(); using var output = new MemoryStream(); input.CopyTo(output);
        byte[] bytes = output.ToArray();
        if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Asset checksum mismatch.");
        return bytes;
    }
    public void Extract(string stage, IProgress<InstallProgress>? progress, CancellationToken cancel) {
        long total = Manifest.Files.Sum(f => f.Length), written = 0;
        foreach (var file in Manifest.Files) {
            cancel.ThrowIfCancellationRequested();
            string destination = Paths.Inside(stage, file.Path); Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using (var input = zip.GetEntry("payload/" + file.Path)!.Open())
            using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
                byte[] buffer = new byte[128 * 1024]; int count;
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                while ((count = input.Read(buffer)) > 0) { cancel.ThrowIfCancellationRequested(); output.Write(buffer, 0, count); hash.AppendData(buffer, 0, count); }
                output.Flush(true);
                if (!Convert.ToHexString(hash.GetHashAndReset()).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException($"Checksum mismatch: {file.Path}");
            }
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(destination, (UnixFileMode)(file.UnixMode & 511));
            written += file.Length;
            progress?.Report(new("verify", file.Path, (int)(written * 45 / Math.Max(1, total))));
        }
        foreach (var asset in Manifest.Assets) ReadAsset(asset.Path);
    }
    public void Dispose() => zip.Dispose();

    public static PackageManifest Build(string configPath, string runtime, string destination) {
        if (!Paths.SupportedRuntimes.Contains(runtime)) throw new InvalidDataException("Unsupported runtime.");
        using var input = File.OpenRead(configPath);
        var config = JsonSerializer.Deserialize(input, InstallerJson.Default.ProductConfig) ?? throw new InvalidDataException("Empty config.");
        Paths.Validate(config);
        if (!config.Applications.TryGetValue(runtime, out var app)) throw new InvalidDataException($"No application configured for {runtime}.");
        string configRoot = Path.GetDirectoryName(Path.GetFullPath(configPath))!;
        string source = Path.GetFullPath(app.Source, configRoot); Paths.NoLinks(source); Paths.Relative(app.EntryPoint);
        if (!Directory.Exists(source)) throw new DirectoryNotFoundException(source);
        string fullDestination = Path.GetFullPath(destination);
        if (fullDestination.StartsWith(source + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new IOException("Package output must be outside application source.");
        var files = new List<PackageFile>(); var assets = new List<PackageFile>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.ReadWrite);
        try {
            using (var archive = new ZipArchive(output, ZipArchiveMode.Create, true)) {
                foreach (string path in EnumerateFiles(source)) {
                    string relative = Path.GetRelativePath(source, path).Replace('\\', '/'); Paths.Relative(relative);
                    if (!names.Add(relative) || Paths.IsData(config, relative) || relative.Split('/')[0].Equals(Paths.StateDirectory, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException($"Colliding, reserved or user-data file: {relative}");
                    int mode = OperatingSystem.IsWindows() ? 420 : (int)File.GetUnixFileMode(path) & 511;
                    if (relative == app.EntryPoint || app.ExecutableFiles.Contains(relative)) mode |= 73;
                    files.Add(Add(archive, path, "payload/" + relative, relative, mode));
                }
                string? Asset(string? path, string name) {
                    if (path == null) return null;
                    string full = Path.GetFullPath(path, configRoot); Paths.NoLinks(full);
                    if (new FileInfo(full).Length > 16 * 1024 * 1024) throw new InvalidDataException("Branding/license exceeds 16 MiB.");
                    string relative = name + Path.GetExtension(full).ToLowerInvariant();
                    assets.Add(Add(archive, full, "assets/" + relative, relative, 420)); return relative;
                }
                var portable = config with { Applications = [], Schema = null,
                    Branding = config.Branding with { Icon = Asset(config.Branding.Icon, "icon"), Cover = Asset(config.Branding.Cover, "cover") },
                    License = Asset(config.License, "license") };
                var manifest = new PackageManifest(1, runtime, portable, app.EntryPoint, files, assets);
                if (!files.Any(f => f.Path == app.EntryPoint)) throw new InvalidDataException("Entry point is missing from application source.");
                using var json = archive.CreateEntry("manifest.json").Open(); JsonSerializer.Serialize(json, manifest, InstallerJson.Default.PackageManifest);
                return manifest;
            }
        } catch { output.Dispose(); File.Delete(destination); throw; }
    }
    private static PackageFile Add(ZipArchive archive, string path, string entryName, string relative, int mode) {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        entry.LastWriteTime = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero); entry.ExternalAttributes = (0x8000 | mode) << 16;
        using var input = File.OpenRead(path); long length = input.Length;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); using var output = entry.Open();
        byte[] buffer = new byte[128 * 1024]; int count;
        while ((count = input.Read(buffer)) > 0) { output.Write(buffer, 0, count); hash.AppendData(buffer, 0, count); }
        return new(relative, length, Convert.ToHexString(hash.GetHashAndReset()), mode);
    }
    public static IEnumerable<string> EnumerateFiles(string root) {
        Paths.NoLinks(root);
        foreach (string entry in Directory.EnumerateFileSystemEntries(root).Order(StringComparer.Ordinal)) {
            Paths.NoLinks(entry);
            if (Directory.Exists(entry)) { foreach (string nested in EnumerateFiles(entry)) yield return nested; }
            else yield return entry;
        }
    }
}
