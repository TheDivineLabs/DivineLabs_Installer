using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
namespace DivineLabs.Installer.Core;

public sealed partial class Engine(IPlatformServices platform, string? workRoot = null) : IInstallerEngine {
    private readonly string scratch = workRoot ?? Path.Combine(Path.GetTempPath(), "DivineLabsInstaller");
    private const string OwnerPath = Paths.StateDirectory + "/owner.json";
    private const string Transaction = Paths.StateDirectory + "/transaction";
    private static string Hash(string file) { using var stream = File.OpenRead(file); return Convert.ToHexString(SHA256.HashData(stream)); }
    private static bool ValidHash(string hash) => hash.Length == 64 && hash.All(Uri.IsHexDigit);
    private static void ValidateOwnedPath(string path) {
        Paths.Relative(path);
        if (path.Split('/')[0].Equals(Paths.StateDirectory, StringComparison.OrdinalIgnoreCase) &&
            !path.StartsWith(Paths.RuntimeDirectory + "/", StringComparison.Ordinal) &&
            !path.StartsWith(Paths.StateDirectory + "/branding/", StringComparison.Ordinal))
            throw new InvalidDataException("Receipt contains an invalid managed path.");
    }
    private static void ValidateReceipt(Receipt receipt) {
        Paths.Validate(receipt.Product);
        if (receipt.ProductId != receipt.Product.Id || !Paths.SupportedRuntimes.Contains(receipt.Runtime)) throw new InvalidDataException("Invalid installation receipt.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in receipt.Files) {
            ValidateOwnedPath(file.Path);
            if (!names.Add(file.Path) || !ValidHash(file.Sha256) || Paths.IsData(receipt.Product, file.Path)) throw new InvalidDataException("Invalid owned file inventory.");
        }
        if (!receipt.Files.Any(f => f.Path == receipt.EntryPoint)) throw new InvalidDataException("Receipt entry point is missing.");
    }
    public static Receipt? ReadReceipt(string target) {
        string path = Paths.Inside(target, Paths.ReceiptPath); Paths.NoLinks(path);
        if (!File.Exists(path)) return null;
        using var stream = File.OpenRead(path);
        var receipt = JsonSerializer.Deserialize(stream, InstallerJson.Default.Receipt) ?? throw new InvalidDataException("Empty receipt.");
        ValidateReceipt(receipt); return receipt;
    }
    private static bool HasOwner(string target, string productId) {
        string path = Paths.Inside(target, OwnerPath); Paths.NoLinks(path);
        if (!File.Exists(path)) return false;
        using var stream = File.OpenRead(path);
        var owner = JsonSerializer.Deserialize(stream, InstallerJson.Default.OwnershipRecord);
        return owner?.FormatVersion == 1 && owner.ProductId == productId;
    }
    private FileStream Acquire(string target) {
        Directory.CreateDirectory(scratch); Paths.NoLinks(scratch);
        string name = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(OperatingSystem.IsWindows() ? target.ToUpperInvariant() : target)));
        string path = Path.Combine(scratch, name + ".lock"); Paths.NoLinks(path);
        try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException e) { throw new IOException("Another operation is using this installation.", e); }
    }
    public OperationResult Install(string packagePath, string target, string? runtimeSource = null, InstallOptions? options = null,
        IProgress<InstallProgress>? progress = null, CancellationToken cancel = default) {
        options ??= new(); target = Paths.Target(target);
        using var package = new Package(packagePath);
        if (package.Manifest.Runtime != System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier)
            throw new InvalidDataException($"This package targets {package.Manifest.Runtime}, not this operating system/architecture.");
        using var operationLock = Acquire(target);
        Recover(target, package.Manifest.Product.Id);
        var previous = ReadReceipt(target);
        if (previous != null && (previous.ProductId != package.Manifest.Product.Id || previous.Runtime != package.Manifest.Runtime))
            throw new IOException("This folder belongs to another product or architecture.");
        if (previous == null && !HasOwner(target, package.Manifest.Product.Id) && Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any(p => Path.GetFileName(p) != Paths.StateDirectory))
            throw new IOException("The selected folder is occupied. Choose an empty application directory.");
        if (previous != null && !previous.Product.DataDirectories.SequenceEqual(package.Manifest.Product.DataDirectories, StringComparer.OrdinalIgnoreCase))
            throw new IOException("Changing dataDirectories requires an explicit migration; update refused.");
        string txn = Paths.Inside(target, Transaction), stage = Path.Combine(txn, "stage");
        Paths.NoLinks(txn); Directory.CreateDirectory(stage);
        bool journalWritten = false;
        try {
            package.Extract(stage, progress, cancel);
            var files = package.Manifest.Files.Select(f => new OwnedFile(f.Path, f.Sha256)).ToList();
            foreach (var asset in package.Manifest.Assets) {
                string relative = Paths.StateDirectory + "/branding/" + asset.Path;
                string destination = Paths.Inside(stage, relative); Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.WriteAllBytes(destination, package.ReadAsset(asset.Path)!); files.Add(new(relative, asset.Sha256));
            }
            if (runtimeSource != null) {
                foreach (string source in Package.EnumerateFiles(runtimeSource)) {
                    cancel.ThrowIfCancellationRequested();
                    string relative = Path.GetRelativePath(runtimeSource, source).Replace('\\', '/');
                    if (!Package.IsRuntimeFile(relative)) continue;
                    relative = Paths.RuntimeDirectory + "/" + relative; string destination = Paths.Inside(stage, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!); File.Copy(source, destination);
                    if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(destination, File.GetUnixFileMode(source));
                    files.Add(new(relative, Hash(destination)));
                }
            }
            var next = new Receipt(package.Manifest.Product.Id, package.Manifest.Product.Version, package.Manifest.Runtime,
                package.Manifest.Product, package.Manifest.EntryPoint, files, options.Integrations, options.DesktopShortcut, options.MenuShortcut);
            ValidateReceipt(next);
            var owned = previous?.Files.Select(f => f.Path).ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
            foreach (var file in files) {
                string destination = Paths.Inside(target, file.Path); Paths.NoLinks(destination);
                if ((File.Exists(destination) || Directory.Exists(destination)) && !owned.Contains(file.Path))
                    throw new IOException($"Update would replace an unowned file: {file.Path}");
            }
            var items = files.Select(f => new JournalItem(f.Path, File.Exists(Paths.Inside(target, f.Path)), f.Sha256)).ToList();
            var desired = files.Select(f => f.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var file in previous?.Files ?? []) if (!desired.Contains(file.Path)) items.Add(new(file.Path, File.Exists(Paths.Inside(target, file.Path)), null));
            Write(Path.Combine(txn, "journal.json"), new TransactionJournal(next.ProductId, "applying", previous, items), InstallerJson.Default.TransactionJournal);
            journalWritten = true;
            Apply(target, items, cancel, progress);
            Write(Paths.Inside(target, Paths.ReceiptPath), next, InstallerJson.Default.Receipt);
            Write(Paths.Inside(target, OwnerPath), new OwnershipRecord(1, next.ProductId), InstallerJson.Default.OwnershipRecord);
            Write(Path.Combine(txn, "journal.json"), new TransactionJournal(next.ProductId, "committed", previous, items), InstallerJson.Default.TransactionJournal);
            Paths.DeleteOwnedTree(txn);
            var warnings = new List<string>();
            if (options.Integrations) TryIntegration(() => platform.Register(target, next), warnings);
            progress?.Report(new("complete", next.Product.Name, 100)); return new(warnings);
        } catch {
            if (journalWritten) Recover(target, package.Manifest.Product.Id);
            else if (Directory.Exists(txn)) Paths.DeleteOwnedTree(txn);
            throw;
        }
    }
    public OperationResult Uninstall(string target, bool removeData = false, IProgress<InstallProgress>? progress = null,
        CancellationToken cancel = default) {
        target = Paths.Target(target); using var operationLock = Acquire(target);
        RecoverInterruptedInternal(target);
        var initial = ReadReceipt(target) ?? throw new IOException("No DivineLabs installation receipt in this folder.");
        Recover(target, initial.ProductId);
        var receipt = ReadReceipt(target) ?? throw new IOException("Installation no longer exists.");
        string txn = Paths.Inside(target, Transaction); Paths.NoLinks(txn); Directory.CreateDirectory(txn);
        foreach (var file in receipt.Files) Paths.NoLinks(Paths.Inside(target, file.Path));
        if (removeData) foreach (string data in receipt.Product.DataDirectories) {
            string directory = Paths.Inside(target, data); Paths.NoLinks(directory);
            if (Directory.Exists(directory)) foreach (string file in Package.EnumerateFiles(directory)) Paths.NoLinks(file);
        }
        var items = receipt.Files.Select(f => new JournalItem(f.Path, File.Exists(Paths.Inside(target, f.Path)), null)).ToList();
        Write(Path.Combine(txn, "journal.json"), new TransactionJournal(receipt.ProductId, "applying", receipt, items), InstallerJson.Default.TransactionJournal);
        try {
            Apply(target, items, cancel, progress); cancel.ThrowIfCancellationRequested();
            File.Delete(Paths.Inside(target, Paths.ReceiptPath));
            Write(Path.Combine(txn, "journal.json"), new TransactionJournal(receipt.ProductId, "committed", receipt, items), InstallerJson.Default.TransactionJournal);
        } catch { Recover(target, receipt.ProductId); throw; }
        Paths.DeleteOwnedTree(txn);
        var warnings = new List<string>();
        if (receipt.Integrations) TryIntegration(() => platform.Unregister(target, receipt), warnings);
        if (removeData) foreach (string data in receipt.Product.DataDirectories) {
            string path = Paths.Inside(target, data);
            if (Directory.Exists(path)) Paths.DeleteOwnedTree(path); else if (File.Exists(path)) File.Delete(path);
        }
        bool userContent = Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Where(p => Path.GetFileName(p) != Paths.StateDirectory).Any(p => File.Exists(p) || (Directory.Exists(p) && ((File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0 || HasRetainedContent(p))));
        string owner = Paths.Inside(target, OwnerPath);
        if (userContent) Write(owner, new OwnershipRecord(1, receipt.ProductId), InstallerJson.Default.OwnershipRecord);
        else if (File.Exists(owner)) File.Delete(owner);
        // Remove only empty directories; unrelated files and default-preserved data remain.
        if (Directory.Exists(target)) {
            var directories = SafeDirectories(target).OrderByDescending(p => p.Length).ToArray();
            foreach (string directory in directories) if (!Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
            if (!Directory.EnumerateFileSystemEntries(target).Any()) Directory.Delete(target);
        }
        progress?.Report(new("complete", receipt.Product.Name, 100)); return new(warnings);
    }
    private static bool HasRetainedContent(string root) => Directory.EnumerateFileSystemEntries(root).Any(p =>
        (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0 || File.Exists(p) || (Directory.Exists(p) && HasRetainedContent(p)));
    private static IEnumerable<string> SafeDirectories(string root) {
        foreach (string directory in Directory.EnumerateDirectories(root)) {
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) continue;
            yield return directory; foreach (string nested in SafeDirectories(directory)) yield return nested;
        }
    }
    public void RecoverInterrupted(string target) {
        target = Paths.Target(target); using var operationLock = Acquire(target); RecoverInterruptedInternal(target);
    }
    private void RecoverInterruptedInternal(string target) {
        string path = Paths.Inside(target, Transaction + "/journal.json"); Paths.NoLinks(path);
        if (!File.Exists(path)) return;
        using var stream = File.OpenRead(path);
        var journal = JsonSerializer.Deserialize(stream, InstallerJson.Default.TransactionJournal) ?? throw new InvalidDataException("Empty journal.");
        stream.Dispose(); Recover(target, journal.ProductId);
    }
    private static void Apply(string target, List<JournalItem> items, CancellationToken cancel, IProgress<InstallProgress>? progress) {
        string txn = Paths.Inside(target, Transaction);
        for (int i = 0; i < items.Count; i++) {
            cancel.ThrowIfCancellationRequested(); var item = items[i]; string destination = Paths.Inside(target, item.Path); Paths.NoLinks(destination);
            if (item.HadOriginal) {
                string backup = Paths.Inside(Path.Combine(txn, "backup"), item.Path); Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                File.Move(destination, backup);
            }
            if (item.NewHash != null) {
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Move(Paths.Inside(Path.Combine(txn, "stage"), item.Path), destination);
            }
            progress?.Report(new(item.NewHash == null ? "remove" : "install", item.Path, 45 + (i + 1) * 50 / Math.Max(1, items.Count)));
        }
        cancel.ThrowIfCancellationRequested();
    }
    private void Recover(string target, string productId) {
        string txn = Paths.Inside(target, Transaction), journalPath = Path.Combine(txn, "journal.json"); Paths.NoLinks(txn);
        if (!Directory.Exists(txn)) return;
        if (!File.Exists(journalPath)) { Paths.DeleteOwnedTree(txn); return; }
        using var stream = File.OpenRead(journalPath);
        var journal = JsonSerializer.Deserialize(stream, InstallerJson.Default.TransactionJournal) ?? throw new InvalidDataException("Invalid recovery journal.");
        stream.Dispose();
        if (journal.ProductId != productId || journal.State is not ("applying" or "committed")) throw new InvalidDataException("Recovery journal belongs to another product.");
        if (journal.PreviousReceipt != null) { ValidateReceipt(journal.PreviousReceipt); if (journal.PreviousReceipt.ProductId != productId) throw new InvalidDataException("Recovery identity mismatch."); }
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in journal.Items) {
            ValidateOwnedPath(item.Path);
            if (!unique.Add(item.Path) || (item.NewHash != null && !ValidHash(item.NewHash))) throw new InvalidDataException("Invalid recovery inventory.");
            Paths.NoLinks(Paths.Inside(target, item.Path)); Paths.NoLinks(Paths.Inside(Path.Combine(txn, "backup"), item.Path));
        }
        if (journal.State == "applying") {
            foreach (var item in journal.Items.AsEnumerable().Reverse()) {
                string destination = Paths.Inside(target, item.Path), backup = Paths.Inside(Path.Combine(txn, "backup"), item.Path);
                if (File.Exists(backup)) {
                    if (File.Exists(destination)) {
                        if (item.NewHash == null || !Hash(destination).Equals(item.NewHash, StringComparison.OrdinalIgnoreCase))
                            throw new IOException("Recovery found an externally modified file. Transaction retained for inspection.");
                        File.Delete(destination);
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!); File.Move(backup, destination);
                } else if (!item.HadOriginal && item.NewHash != null && File.Exists(destination)) {
                    if (!Hash(destination).Equals(item.NewHash, StringComparison.OrdinalIgnoreCase)) throw new IOException("Recovery found an unowned replacement.");
                    File.Delete(destination);
                }
            }
            string receipt = Paths.Inside(target, Paths.ReceiptPath);
            if (journal.PreviousReceipt == null) File.Delete(receipt);
            else Write(receipt, journal.PreviousReceipt, InstallerJson.Default.Receipt);
        }
        Paths.DeleteOwnedTree(txn);
    }
    private static void TryIntegration(Action action, List<string> warnings) { try { action(); } catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { warnings.Add(e.Message); } }
    private static void Write<T>(string path, T value, JsonTypeInfo<T> type) {
        Paths.NoLinks(path); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { JsonSerializer.Serialize(output, value, type); output.Flush(true); } File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
