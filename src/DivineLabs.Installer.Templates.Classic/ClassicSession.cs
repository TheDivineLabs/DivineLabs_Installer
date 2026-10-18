using System.Text;
using DivineLabs.Installer.Core;
namespace DivineLabs.Installer.Templates.Classic;

/// <summary>Owns package assets for a Classic window. The host owns and disposes this session after the window closes.</summary>
public sealed class ClassicSession : IDisposable {
    public ProductConfig Product { get; }
    public Package? Package { get; }
    public bool Uninstall { get; }
    public string Target { get; }
    public IPlatformServices Platform { get; }
    public IInstallerEngine Engine { get; }
    public string? PackagePath { get; }
    public string? RuntimeDirectory { get; }
    public InstallOptions Options { get; }
    private ClassicSession(ProductConfig product, string? packagePath, string? target, bool uninstall,
        IInstallerEngine engine, IPlatformServices platform, string? runtimeDirectory, InstallOptions? options = null) {
        ArgumentNullException.ThrowIfNull(engine); ArgumentNullException.ThrowIfNull(platform);
        Product = product; PackagePath = packagePath; Uninstall = uninstall; Engine = engine;
        Platform = platform; RuntimeDirectory = runtimeDirectory; Options = options ?? new(false);
        Target = target ?? platform.DefaultDirectory(product);
        if (packagePath != null) Package = new Package(packagePath);
    }
    public static ClassicSession OpenPackage(string packagePath, IInstallerEngine engine, IPlatformServices platform,
        string? targetDirectory = null, string? runtimeDirectory = null, InstallOptions? options = null) {
        using var package = new Package(packagePath);
        if (package.Manifest.Product.Template != "divinelabs-classic") throw new InvalidDataException("This package requires a different template.");
        return new(package.Manifest.Product, packagePath, targetDirectory, false, engine, platform, runtimeDirectory, options);
    }
    public static async Task<ClassicSession> OpenInstallationAsync(string targetDirectory, IInstallerEngine engine,
        IPlatformServices platform, CancellationToken cancellationToken = default) {
        await engine.RecoverAsync(targetDirectory, cancellationToken).ConfigureAwait(false);
        var receipt = engine.GetInstallation(targetDirectory) ?? throw new IOException("No installation receipt found.");
        return new(receipt.Product, null, targetDirectory, true, engine, platform, null);
    }
    public static ClassicSession Preview(IInstallerEngine engine, IPlatformServices platform) => new(new() {
        Id = "org.divinelabs.example", Name = "DivineLabs Installer", Publisher = "DivineLabs", Version = "0.2.1", Language = "en",
        Description = "Your application. One configuration. Every desktop.", Links = [new("GitHub", "https://github.com/TheDivineLabs/DivineLabs_Installer"), new("Documentation", "https://github.com/TheDivineLabs/DivineLabs_Installer#readme")]
    }, null, null, false, engine, platform, null, new());
    public IEnumerable<(string Folder, string Version)> FindInstallations() =>
        Platform is IInstallationLocator locator ? locator.FindInstallations(Product) : [];
    public byte[]? Asset(string? name) {
        if (name == null) return null;
        if (Package != null) return Package.ReadAsset(name);
        string path = Paths.Inside(Target, Paths.StateDirectory + "/branding/" + name); Paths.NoLinks(path);
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }
    public string License => Asset(Product.License) is { } bytes ? Encoding.UTF8.GetString(bytes) : "MIT License\n\nCopyright (c) 2026 DivineLabs\n\nThis installer is free and open-source software.\nhttps://github.com/TheDivineLabs/DivineLabs_Installer";
    public void Dispose() => Package?.Dispose();
}
