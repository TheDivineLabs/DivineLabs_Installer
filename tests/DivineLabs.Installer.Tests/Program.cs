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
string first = Make("1.0.0", (entry, "first"), ("obsolete.txt", "old"));
engine.Install(first, target, options: new(false));
Check(File.ReadAllText(Path.Combine(target, entry)) == "first", "install extracts verified payload");
if (!OperatingSystem.IsWindows()) Check((File.GetUnixFileMode(Path.Combine(target, entry)) & UnixFileMode.UserExecute) != 0, "entry point retains executable mode");
Directory.CreateDirectory(Path.Combine(target, "data")); File.WriteAllText(Path.Combine(target, "data", "settings.json"), "user data"); File.WriteAllText(Path.Combine(target, "personal.txt"), "foreign");
string update = Make("1.1.0", (entry, "second"), ("new.txt", "new"));
engine.Install(update, target, options: new(false));
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
    try { engine.Install(cancelPackage, target, options: new(false), progress: new Callback(p => { if (p.Phase == "install") cancel.Cancel(); }), cancel: cancel.Token); throw new Exception("Cancellation did not occur"); }
    catch (OperationCanceledException) { }
}
Check(File.ReadAllText(Path.Combine(target, entry)) == "second" && Engine.ReadReceipt(target)?.Version == "1.1.0", "cancellation after a file move restores files and receipt");
var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
foreach (string argument in new[] { "--crash-child", cancelPackage, target }) start.ArgumentList.Add(argument);
using (var child = Process.Start(start)!) { child.WaitForExit(); Check(child.ExitCode == 99, "crash fixture terminates during file replacement"); }
engine.RecoverInterrupted(target);
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
engine.Uninstall(target);
Check(!File.Exists(Path.Combine(target, entry)) && File.ReadAllText(Path.Combine(target, "data", "settings.json")) == "user data", "default uninstall removes application and retains user data");
Check(File.ReadAllText(Path.Combine(target, "personal.txt")) == "foreign", "uninstall retains foreign files");
engine.Install(first, target, options: new(false));
Check(File.ReadAllText(Path.Combine(target, "data", "settings.json")) == "user data", "reinstall recognizes retained ownership without replacing user data");
engine.Uninstall(target, true);
Check(!Directory.Exists(Path.Combine(target, "data")) && File.ReadAllText(Path.Combine(target, "personal.txt")) == "foreign", "explicit data removal still preserves unrelated files");
string occupied = Path.Combine(work, "occupied"); Directory.CreateDirectory(occupied); File.WriteAllText(Path.Combine(occupied, "keep.txt"), "keep");
Refused(() => engine.Install(first, occupied, options: new(false)), "fresh install refuses occupied directories");
Console.WriteLine($"RESULT: {passed} passed, {skipped} skipped; runtime={rid}; no integrations or application launch. Evidence: {work}");
return 0;
sealed class Callback(Action<InstallProgress> action) : IProgress<InstallProgress> { public void Report(InstallProgress value) => action(value); }
