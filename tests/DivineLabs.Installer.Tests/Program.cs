using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using DivineLabs.Installer.Core;
using DivineLabs.Installer.Platforms;

if (args.Length > 0 && args[0] == "--crash-child") {
    new Engine(new PlatformServices()).Install(args[1], args[2], options: new(false), progress: new Callback(p => { if (p.Phase == "install") Environment.Exit(99); }));
    return 9;
}
string work = Path.Combine(Environment.GetEnvironmentVariable("INSTALLER_TEST_ROOT") ?? Path.GetTempPath(), "DivineLabs-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(work); int passed = 0, skipped = 0;
var engine = new Engine(new PlatformServices(), Path.Combine(work, "locks"));
IInstallerEngine api = engine;
string target = Path.Combine(work, "installation"), source = Path.Combine(work, "source"); Directory.CreateDirectory(source);
string rid = RuntimeInformation.RuntimeIdentifier, entry = OperatingSystem.IsWindows() ? "app.cmd" : "app.sh";
var product = new ProductConfig { Id = "org.divinelabs.contract-test", Name = "Contract Test", Publisher = "DivineLabs", Version = "1.0.0", Applications = new() { [rid] = new() { Source = source, EntryPoint = entry } } };
string Make(string version, params (string Name, string Content)[] files) {
    foreach (string path in Directory.GetFiles(source)) File.Delete(path);
    foreach (var (name, content) in files) File.WriteAllText(Path.Combine(source, name), content);
    string config = Path.Combine(work, "config-" + version + ".json"); File.WriteAllText(config, JsonSerializer.Serialize(product with { Version = version }, InstallerJson.Default.ProductConfig));
    string package = Path.Combine(work, version + ".dlpack"); Package.Build(config, rid, package); return package;
}
void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL: " + name); passed++; Console.WriteLine("PASS: " + name); }
void Refused(Action action, string name) { bool refused = false; try { action(); } catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException) { refused = true; } Check(refused, name); }
var minimalJson = """{"id":"org.example.minimal","name":"Minimal App","publisher":"Example","version":"1.0.0","applications":{"runtime-placeholder":{"entryPoint":"app"}},"branding":{}}""".Replace("runtime-placeholder", rid);
var minimal = JsonSerializer.Deserialize(minimalJson, InstallerJson.Default.ProductConfig)!;
Paths.Validate(minimal);
Check(minimal.SchemaVersion == 1 && minimal.Template == "divinelabs-classic" && minimal.Language == "en" &&
    minimal.Branding.ShowFrameworkBranding && minimal.Branding.Accent == "#6658E8" && minimal.DataDirectories is ["data", "logs"] &&
    minimal.Applications[rid].Source == "application" && minimal.Applications[rid].ExecutableFiles.Length == 0,
    "source-generated reader preserves omitted root and nested configuration defaults");
string first = Make("1.0.0", (entry, "first"), ("obsolete.txt", "old"));
string legacyPackage = Path.Combine(work, "legacy-0.1.dlpack"); File.Copy(first, legacyPackage);
using (var zip = ZipFile.Open(legacyPackage, ZipArchiveMode.Update)) {
    var manifestEntry = zip.GetEntry("manifest.json")!; string manifestJson;
    using (var reader = new StreamReader(manifestEntry.Open())) manifestJson = reader.ReadToEnd();
    const string templateField = "\"template\": \"divinelabs-classic\",";
    if (!manifestJson.Contains(templateField)) throw new Exception("Legacy fixture did not find template metadata.");
    manifestEntry.Delete(); using var writer = new StreamWriter(zip.CreateEntry("manifest.json").Open());
    writer.Write(manifestJson.Replace(templateField, ""));
}
string legacyTarget = Path.Combine(work, "legacy-installation");
await api.InstallAsync(new(legacyPackage, legacyTarget, Options: new(false)));
Check(api.GetInstallation(legacyTarget)?.Product.Template == "divinelabs-classic", "public API installs legacy package metadata without template identity");
string legacyReceiptPath = Paths.Inside(legacyTarget, Paths.ReceiptPath);
File.WriteAllText(legacyReceiptPath, File.ReadAllText(legacyReceiptPath).Replace("\"template\": \"divinelabs-classic\",", ""));
Check(api.GetInstallation(legacyTarget)?.Product.Template == "divinelabs-classic", "public API reads legacy receipt metadata without template identity");
await api.UninstallAsync(new(legacyTarget));
File.WriteAllText(Path.Combine(source, "icon.bin"), "same asset and payload");
string brandingConfig = Path.Combine(work, "branding.json");
File.WriteAllText(brandingConfig, JsonSerializer.Serialize(product with { Branding = new() { Icon = Path.Combine(source, "icon.bin") } }, InstallerJson.Default.ProductConfig));
string brandingPackage = Path.Combine(work, "branding.dlpack"); Package.Build(brandingConfig, rid, brandingPackage);
using (var branded = new Package(brandingPackage)) Check(branded.ReadAsset(branded.Manifest.Product.Branding.Icon)!.Length > 0, "identical branding and payload assets remain distinct archive entries");
if (OperatingSystem.IsMacOS()) Check(Paths.CanonicalHostPath("/var/folders").StartsWith("/private/var/", StringComparison.Ordinal), "macOS system temporary-directory alias resolves without trusting app symlinks");
await api.InstallAsync(new(first, target, Options: new(false)));
Check(File.ReadAllText(Path.Combine(target, entry)) == "first", "install extracts verified payload");
if (!OperatingSystem.IsWindows()) Check((File.GetUnixFileMode(Path.Combine(target, entry)) & UnixFileMode.UserExecute) != 0, "entry point retains executable mode");
if (OperatingSystem.IsLinux()) {
    string xdg = Path.Combine(work, "xdg"), desktop = Path.Combine(work, "desktop"); Directory.CreateDirectory(desktop);
    var isolatedPlatform = new PlatformServices(xdg, desktop);
    var desktopOnly = Engine.ReadReceipt(target)! with { DesktopShortcut = true, MenuShortcut = false };
    isolatedPlatform.Register(target, desktopOnly);
    Check(File.Exists(Path.Combine(desktop, product.Id + ".desktop")) && !Directory.Exists(Path.Combine(xdg, "applications")), "Linux desktop shortcut is independent of application-menu option");
    isolatedPlatform.Unregister(target, desktopOnly);
    Check(!File.Exists(Path.Combine(desktop, product.Id + ".desktop")), "Linux launcher cleanup stays within isolated integration directories");
}
Directory.CreateDirectory(Path.Combine(target, "data")); File.WriteAllText(Path.Combine(target, "data", "settings.json"), "user data"); File.WriteAllText(Path.Combine(target, "personal.txt"), "foreign");
string update = Make("1.1.0", (entry, "second"), ("new.txt", "new"));
await api.InstallAsync(new(update, target, Options: new(false)));
Check(File.ReadAllText(Path.Combine(target, entry)) == "second" && !File.Exists(Path.Combine(target, "obsolete.txt")), "update replaces owned files and removes obsolete owned files");
Check(File.ReadAllText(Path.Combine(target, "data", "settings.json")) == "user data" && File.ReadAllText(Path.Combine(target, "personal.txt")) == "foreign", "update preserves data and unowned files");
string collision = Make("1.2.0", (entry, "third"), ("personal.txt", "replace foreign"));
Refused(() => engine.Install(collision, target, options: new(false)), "update refuses unowned collision");
Check(File.ReadAllText(Path.Combine(target, entry)) == "second", "collision is rejected before any application file changes");
string corrupt = Path.Combine(work, "corrupt.dlpack"); File.Copy(update, corrupt);
using (var zip = ZipFile.Open(corrupt, ZipArchiveMode.Update)) { var old = zip.GetEntry("payload/" + entry)!; old.Delete(); using var writer = new StreamWriter(zip.CreateEntry("payload/" + entry).Open()); writer.Write("broken"); }
Refused(() => engine.Install(corrupt, target, options: new(false)), "checksum mismatch is rejected");
Check(File.ReadAllText(Path.Combine(target, entry)) == "second", "corrupt package leaves existing installation intact");
string traversal = Path.Combine(work, "traversal.dlpack"); File.Copy(update, traversal);
using (var zip = ZipFile.Open(traversal, ZipArchiveMode.Update)) { using var writer = new StreamWriter(zip.CreateEntry("../escaped.txt").Open()); writer.Write("bad"); }
Refused(() => engine.Install(traversal, target, options: new(false)), "archive traversal is rejected");
Check(!File.Exists(Path.Combine(work, "escaped.txt")), "archive cannot escape installation root");
string cancelPackage = Make("1.3.0", (entry, "cancelled"), ("other.txt", "cancelled"));
using (var cancel = new CancellationTokenSource()) {
    try { await api.InstallAsync(new(cancelPackage, target, Options: new(false)), new Callback(p => { if (p.Phase == "install") cancel.Cancel(); }), cancel.Token); throw new Exception("Cancellation did not occur"); }
    catch (OperationCanceledException) { }
}
Check(File.ReadAllText(Path.Combine(target, entry)) == "second" && Engine.ReadReceipt(target)?.Version == "1.1.0", "cancellation after a file move restores files and receipt");
var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
foreach (string argument in new[] { "--crash-child", cancelPackage, target }) start.ArgumentList.Add(argument);
using (var child = Process.Start(start)!) { child.WaitForExit(); Check(child.ExitCode == 99, "crash fixture terminates during file replacement"); }
await api.RecoverAsync(target);
Check(File.ReadAllText(Path.Combine(target, entry)) == "second" && Engine.ReadReceipt(target)?.Version == "1.1.0", "restart recovery restores interrupted transaction");
string receiptPath = Paths.Inside(target, Paths.ReceiptPath); string originalReceipt = File.ReadAllText(receiptPath);
var receipt = Engine.ReadReceipt(target)!;
File.WriteAllText(receiptPath, JsonSerializer.Serialize(receipt with { Files = [.. receipt.Files, new("../personal.txt", new string('A', 64))] }, InstallerJson.Default.Receipt));
Refused(() => engine.Uninstall(target), "tampered uninstall inventory cannot traverse outside target"); File.WriteAllText(receiptPath, originalReceipt);
try {
    string link = Path.Combine(work, "linked-installation"); Directory.CreateSymbolicLink(link, target);
    Refused(() => engine.Install(update, link, options: new(false)), "linked target is refused"); Directory.Delete(link);
} catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or PlatformNotSupportedException) { skipped++; Console.WriteLine("SKIP: symbolic-link creation requires host permission"); }
if (OperatingSystem.IsWindows()) {
    using var held = new FileStream(Path.Combine(target, entry), FileMode.Open, FileAccess.Read, FileShare.None);
    Refused(() => engine.Install(update, target, options: new(false)), "locked application file refuses update safely");
}
await api.UninstallAsync(new(target));
Check(!File.Exists(Path.Combine(target, entry)) && File.ReadAllText(Path.Combine(target, "data", "settings.json")) == "user data", "default uninstall removes application and retains user data");
Check(File.ReadAllText(Path.Combine(target, "personal.txt")) == "foreign", "uninstall retains foreign files");
engine.Install(first, target, options: new(false));
Check(File.ReadAllText(Path.Combine(target, "data", "settings.json")) == "user data", "reinstall recognizes retained ownership without replacing user data");
engine.Uninstall(target, true);
Check(!Directory.Exists(Path.Combine(target, "data")) && File.ReadAllText(Path.Combine(target, "personal.txt")) == "foreign", "explicit data removal still preserves unrelated files");
string occupied = Path.Combine(work, "occupied"); Directory.CreateDirectory(occupied); File.WriteAllText(Path.Combine(occupied, "keep.txt"), "keep");
Refused(() => engine.Install(first, occupied, options: new(false)), "fresh install refuses occupied directories");
using (var cancelled = new CancellationTokenSource()) {
    cancelled.Cancel(); string untouched = Path.Combine(work, "pre-cancelled");
    try { await api.InstallAsync(new(first, untouched, Options: new(false)), cancellationToken: cancelled.Token); throw new Exception("Expected cancellation."); }
    catch (OperationCanceledException) { }
    Check(!Directory.Exists(untouched), "pre-cancelled public API performs no filesystem mutation");
}
try { await api.InstallAsync(new(first, occupied, Options: new(false))); throw new Exception("Expected ownership failure."); }
catch (IOException) { }
Check(File.ReadAllText(Path.Combine(occupied, "keep.txt")) == "keep", "public API propagates ownership failure without replacing user files");
Check(api.GetInstallation(target) == null, "public API reports absent receipt after removal");
Console.WriteLine($"RESULT: {passed} passed, {skipped} skipped; runtime={rid}; no integrations or application launch. Evidence: {work}");
return 0;
sealed class Callback(Action<InstallProgress> action) : IProgress<InstallProgress> { public void Report(InstallProgress value) => action(value); }
