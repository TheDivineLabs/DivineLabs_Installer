using System.Text.Json;
using System.Text.Json.Serialization;
namespace DivineLabs.Installer.Core;

public sealed record ApplicationSource {
    public string Source { get; set; } = "application";
    public string EntryPoint { get; set; } = "";
    public string[] ExecutableFiles { get; set; } = [];
}
public sealed record Branding {
    public bool ShowFrameworkBranding { get; set; } = true;
    public string? Icon { get; set; }
    public string? Cover { get; set; }
    public string Accent { get; set; } = "#6658E8";
}
public sealed record ProductLink(string Label, string Url);
public sealed record ProductConfig {
    [JsonPropertyName("$schema")] public string? Schema { get; set; }
    public int SchemaVersion { get; set; } = 1;
    public string Template { get; set; } = "divinelabs-classic";
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Publisher { get; set; } = "";
    public string Version { get; set; } = "";
    public string Description { get; set; } = "";
    public string InstallDirectoryName { get; set; } = "";
    public string? License { get; set; }
    public string Language { get; set; } = "en";
    public Branding Branding { get; set; } = new();
    public Dictionary<string, ApplicationSource> Applications { get; set; } = [];
    public ProductLink[] Links { get; set; } = [];
    public string[] DataDirectories { get; set; } = ["data", "logs"];
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
public sealed record RuntimeInfo(int FormatVersion, string Runtime, string EntryPoint, bool NativeAot, string Template = "divinelabs-classic");
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
