using System.Text.Json;
using System.Text.Json.Serialization;
namespace DivineLabs.Installer.Core;

public sealed record ApplicationSource {
    public string Source { get; init; } = "application";
    public string EntryPoint { get; init; } = "";
    public string[] ExecutableFiles { get; init; } = [];
}
public sealed record Branding {
    public string? Icon { get; init; }
    public string? Cover { get; init; }
    public string Accent { get; init; } = "#6658E8";
}
public sealed record ProductLink(string Label, string Url);
public sealed record ProductConfig {
    [JsonPropertyName("$schema")] public string? Schema { get; init; }
    public int SchemaVersion { get; init; } = 1;
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Publisher { get; init; } = "";
    public string Version { get; init; } = "";
    public string Description { get; init; } = "";
    public string InstallDirectoryName { get; init; } = "";
    public string? License { get; init; }
    public string Language { get; init; } = "en";
    public Branding Branding { get; init; } = new();
    public Dictionary<string, ApplicationSource> Applications { get; init; } = [];
    public ProductLink[] Links { get; init; } = [];
    public string[] DataDirectories { get; init; } = ["data", "logs"];
}
public sealed record PackageFile(string Path, long Length, string Sha256, int UnixMode = 420);
public sealed record PackageManifest(int FormatVersion, string Runtime, ProductConfig Product, string EntryPoint,
    List<PackageFile> Files, List<PackageFile> Assets);
public sealed record OwnershipRecord(int FormatVersion, string ProductId);
public sealed record OwnedFile(string Path, string Sha256);
public sealed record Receipt(string ProductId, string Version, string Runtime, ProductConfig Product,
    string EntryPoint, List<OwnedFile> Files, bool Integrations, bool DesktopShortcut, bool MenuShortcut);
public sealed record JournalItem(string Path, bool HadOriginal, string? NewHash);
public sealed record TransactionJournal(string ProductId, string State, Receipt? PreviousReceipt, List<JournalItem> Items);
public sealed record InstallOptions(bool Integrations = true, bool DesktopShortcut = true, bool MenuShortcut = true);
public sealed record InstallProgress(string Phase, string Detail, int Percent);
public sealed record RuntimeInfo(int FormatVersion, string Runtime, string EntryPoint, bool NativeAot);
public sealed record OperationResult(List<string> Warnings);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(ProductConfig))]
[JsonSerializable(typeof(PackageManifest))]
[JsonSerializable(typeof(Receipt))]
[JsonSerializable(typeof(OwnershipRecord))]
[JsonSerializable(typeof(TransactionJournal))]
[JsonSerializable(typeof(RuntimeInfo))]
public partial class InstallerJson : JsonSerializerContext { }

public interface IPlatformServices {
    string DefaultDirectory(ProductConfig product);
    void Register(string root, Receipt receipt);
    void Unregister(string root, Receipt receipt);
    void Launch(string root, string entryPoint);
}
