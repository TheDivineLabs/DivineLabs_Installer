using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security;
using System.Text.Json;
using DivineLabs.Installer.Core;
using DivineLabs.Installer.Platforms;

namespace DivineLabs.Installer.Cli;
internal static class Program {
    public static int Main(string[] args) {
        try {
            if (args.Length == 0 || args[0] is "--help" or "help") { Help(); return 0; }
            if (args[0] == "--version") { Console.WriteLine($"DivineLabs Installer {typeof(Engine).Assembly.GetName().Version?.ToString(3)}"); return 0; }
            string command = args[0]; var options = Options(args.Skip(1).ToArray());
            string Get(string name, string? fallback = null) => options.TryGetValue(name, out string? value) ? value ?? throw new ArgumentException($"{name} requires a value.") : fallback ?? throw new ArgumentException($"Missing {name}.");
            switch (command) {
                case "init": Init(Get("--dir", ".")); break;
                case "validate": {
                    string path = Get("--config", "installer.json");
                    using var input = File.OpenRead(path);
                    var config = JsonSerializer.Deserialize(input, InstallerJson.Default.ProductConfig) ?? throw new InvalidDataException("Empty config.");
                    Paths.Validate(config);
                    if (config.Applications.Count == 0 || config.Applications.Any(p => !Paths.SupportedRuntimes.Contains(p.Key))) throw new InvalidDataException("Configure supported application runtimes.");
                    foreach (var app in config.Applications.Values) { Paths.Relative(app.EntryPoint); Paths.NoLinks(Path.GetFullPath(app.Source, Path.GetDirectoryName(Path.GetFullPath(path))!)); }
                    Console.WriteLine($"Valid: {config.Name} {config.Version}; {config.Applications.Count} runtimes."); break;
                }
                case "pack": {
                    string path = Path.GetFullPath(Get("--output")); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    var manifest = Package.Build(Get("--config", "installer.json"), Get("--runtime", RuntimeInformation.RuntimeIdentifier), path);
                    Console.WriteLine($"Package: {path} ({manifest.Files.Count} files)"); break;
                }
                case "build": Build(Get("--config", "installer.json"), Get("--runtime", RuntimeInformation.RuntimeIdentifier),
                    Get("--runtime-dir", Path.Combine(AppContext.BaseDirectory, "runtime")), Get("--output", "installer-output")); break;
                case "inspect": { using var package = new Package(Get("--package")); Console.WriteLine(JsonSerializer.Serialize(package.Manifest, InstallerJson.Default.PackageManifest)); break; }
                case "install": {
                    string? runtime = options.ContainsKey("--runtime-dir") ? Get("--runtime-dir") : null;
                    IInstallerEngine engine = new Engine(new PlatformServices());
                    var result = engine.InstallAsync(new(Get("--package"), Get("--dir"), runtime,
                        new(!options.ContainsKey("--no-integrations"), !options.ContainsKey("--no-desktop"), !options.ContainsKey("--no-menu"))), new ConsoleProgress()).GetAwaiter().GetResult();
                    foreach (string warning in result.Warnings) Console.Error.WriteLine("Warning: " + warning); break;
                }
                case "uninstall": {
                    IInstallerEngine engine = new Engine(new PlatformServices());
                    var result = engine.UninstallAsync(new(Get("--dir"), options.ContainsKey("--remove-data")), new ConsoleProgress()).GetAwaiter().GetResult();
                    foreach (string warning in result.Warnings) Console.Error.WriteLine("Warning: " + warning); break;
                }
                default: throw new ArgumentException("Unknown command. Run divinepack --help.");
            }
            return 0;
        } catch (OperationCanceledException) { Console.Error.WriteLine("Cancelled; file changes rolled back."); return 3; }
        catch (Exception error) { Console.Error.WriteLine("Error: " + error.Message); return 2; }
    }
    private static Dictionary<string, string?> Options(string[] args) {
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        string[] flags = ["--no-integrations", "--no-desktop", "--no-menu", "--remove-data"];
        string[] values = ["--config", "--runtime", "--runtime-dir", "--output", "--package", "--dir"];
        for (int i = 0; i < args.Length; i++) {
            string key = args[i];
            if (!flags.Contains(key) && !values.Contains(key)) throw new ArgumentException($"Unknown option: {key}");
            string? value = null;
            if (values.Contains(key)) { if (++i >= args.Length || args[i].StartsWith("--")) throw new ArgumentException($"{key} requires a value."); value = args[i]; }
            if (!result.TryAdd(key, value)) throw new ArgumentException($"Duplicate option: {key}");
        }
        return result;
    }
    public static void Init(string directory) {
        directory = Path.GetFullPath(directory); Paths.NoLinks(directory);
        string[] files = ["installer.json", "application/windows/hello.cmd", "application/unix/hello.sh", "LICENSE.txt"];
        if (files.Any(p => File.Exists(Paths.Inside(directory, p)))) throw new IOException("Sample files already exist; nothing was overwritten.");
        Directory.CreateDirectory(Path.Combine(directory, "application", "windows")); Directory.CreateDirectory(Path.Combine(directory, "application", "unix"));
        File.WriteAllText(Path.Combine(directory, "application/windows/hello.cmd"), "@echo off\r\necho Installed with DivineLabs Installer.\r\npause\r\n");
        File.WriteAllText(Path.Combine(directory, "application/unix/hello.sh"), "#!/bin/sh\nprintf 'Installed with DivineLabs Installer.\\n'\n");
        File.WriteAllText(Path.Combine(directory, "LICENSE.txt"), "Example application license. Replace with your application's actual license.\n");
        var applications = new Dictionary<string, ApplicationSource>();
        foreach (string rid in Paths.SupportedRuntimes) applications.Add(rid, new() { Source = rid.StartsWith("win-") ? "application/windows" : "application/unix", EntryPoint = rid.StartsWith("win-") ? "hello.cmd" : "hello.sh" });
        var config = new ProductConfig { Schema = "https://raw.githubusercontent.com/TheDivineLabs/DivineLabs_Installer/main/schemas/installer.schema.json",
            Id = "org.example.hello", Name = "Hello Application", Publisher = "Example", Version = "1.0.0", Description = "A small application, ready to install.", License = "LICENSE.txt", Links = [new("GitHub", "https://github.com/TheDivineLabs/DivineLabs_Installer"), new("Documentation", "https://github.com/TheDivineLabs/DivineLabs_Installer#readme")], Applications = applications };
        File.WriteAllText(Path.Combine(directory, "installer.json"), JsonSerializer.Serialize(config, InstallerJson.Default.ProductConfig));
        Console.WriteLine($"Created {Path.Combine(directory, "installer.json")}");
    }
    private static void Build(string config, string rid, string runtimeDirectory, string output) {
        runtimeDirectory = Path.GetFullPath(runtimeDirectory); Paths.NoLinks(runtimeDirectory);
        using var stream = File.OpenRead(Path.Combine(runtimeDirectory, "runtime.json"));
        var runtime = JsonSerializer.Deserialize(stream, InstallerJson.Default.RuntimeInfo) ?? throw new InvalidDataException("Missing runtime metadata.");
        if (runtime.FormatVersion != 1 || runtime.Runtime != rid || !runtime.NativeAot) throw new InvalidDataException("Use a matching prebuilt Native AOT runtime.");
        Paths.Relative(runtime.EntryPoint);
        if (!File.Exists(Paths.Inside(runtimeDirectory, runtime.EntryPoint))) throw new IOException("Runtime executable is missing.");
        using (var configInput = File.OpenRead(config)) {
            var product = JsonSerializer.Deserialize(configInput, InstallerJson.Default.ProductConfig) ?? throw new InvalidDataException("Empty config.");
            if (product.Template != runtime.Template) throw new InvalidDataException($"Config template {product.Template} does not match runtime template {runtime.Template}.");
        }
        output = Path.GetFullPath(output); Paths.NoLinks(output);
        if (output.StartsWith(runtimeDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new IOException("Output cannot be inside runtime source.");
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("Choose a new, empty output directory.");
        Directory.CreateDirectory(output);
        string pack = Path.Combine(output, Package.FileName);
        var manifest = Package.Build(config, rid, pack);
        string bundleName = manifest.Product.Id + "-" + rid;
        string bundle = Path.Combine(output, rid.StartsWith("osx-") ? bundleName + ".app" : bundleName);
        string executableRoot = rid.StartsWith("osx-") ? Path.Combine(bundle, "Contents", "MacOS") : bundle;
        Directory.CreateDirectory(executableRoot);
        foreach (string source in Package.EnumerateFiles(runtimeDirectory)) {
            string relative = Path.GetRelativePath(runtimeDirectory, source).Replace('\\', '/');
            if (!Package.IsRuntimeFile(relative)) continue;
            string destination = Paths.Inside(executableRoot, relative); Directory.CreateDirectory(Path.GetDirectoryName(destination)!); File.Copy(source, destination);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(destination, File.GetUnixFileMode(source));
        }
        File.Move(pack, Path.Combine(executableRoot, Package.FileName));
        if (rid.StartsWith("osx-")) {
            string E(string s) => SecurityElement.Escape(s) ?? "";
            File.WriteAllText(Path.Combine(bundle, "Contents", "Info.plist"), $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\"><plist version=\"1.0\"><dict><key>CFBundleExecutable</key><string>{E(runtime.EntryPoint)}</string><key>CFBundleIdentifier</key><string>{E(manifest.Product.Id)}.installer</string><key>CFBundleName</key><string>{E(manifest.Product.Name)} Installer</string><key>CFBundlePackageType</key><string>APPL</string><key>CFBundleShortVersionString</key><string>{typeof(Engine).Assembly.GetName().Version?.ToString(3)}</string><key>NSHighResolutionCapable</key><true/></dict></plist>");
        }
        string archive = Path.Combine(output, bundleName + (rid.StartsWith("linux-") ? "-installer.tar.gz" : "-installer.zip"));
        using (var file = new FileStream(archive, FileMode.CreateNew)) {
            if (rid.StartsWith("linux-")) {
                using var gzip = new GZipStream(file, CompressionLevel.Optimal); using var tar = new TarWriter(gzip, leaveOpen: true);
                foreach (string path in Package.EnumerateFiles(bundle)) {
                    string relative = Path.GetRelativePath(output, path).Replace('\\', '/'); using var input = File.OpenRead(path);
                    tar.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, relative) { DataStream = input, Mode = relative.EndsWith(runtime.EntryPoint, StringComparison.Ordinal) ? (UnixFileMode)493 : (UnixFileMode)420, ModificationTime = DateTimeOffset.UnixEpoch });
                }
            } else {
                using var zip = new ZipArchive(file, ZipArchiveMode.Create);
                foreach (string path in Package.EnumerateFiles(bundle)) {
                    string relative = Path.GetRelativePath(output, path).Replace('\\', '/');
                    var entry = zip.CreateEntryFromFile(path, relative, CompressionLevel.Optimal);
                    entry.ExternalAttributes = (0x8000 | (relative.EndsWith(runtime.EntryPoint, StringComparison.Ordinal) ? 493 : 420)) << 16;
                }
            }
        }
        Console.WriteLine($"Installer: {archive}\nExtract and run {runtime.EntryPoint}; keep its native libraries alongside it.");
    }
    private sealed class ConsoleProgress : IProgress<InstallProgress> { public void Report(InstallProgress value) => Console.WriteLine($"{value.Percent,3}% {value.Phase}: {value.Detail}"); }
    private static void Help() => Console.WriteLine("""
DivineLabs Installer 0.2.0 — cross-platform, config-driven application installers
  divinepack init [--dir DIR]
  divinepack validate [--config installer.json]
  divinepack pack --runtime RID --output application.dlpack [--config installer.json]
  divinepack build --runtime RID --output DIR [--runtime-dir DIR] [--config installer.json]
  divinepack inspect --package FILE
  divinepack install --package FILE --dir DIR [--runtime-dir DIR] [--no-integrations]
  divinepack uninstall --dir DIR [--remove-data]
Runtimes: win-x64, win-arm64, osx-x64, osx-arm64, linux-x64, linux-arm64.
""");
}
