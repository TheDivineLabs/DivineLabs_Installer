using System.Diagnostics;
using System.Text;
using DivineLabs.Installer.Core;
using DivineLabs.Installer.Platforms;
namespace DivineLabs.Installer.Desktop;

public sealed class Session : IDisposable {
    public ProductConfig Product { get; }
    public Package? Package { get; }
    public bool Uninstall { get; }
    public string Target { get; }
    public PlatformServices Platform { get; } = new();
    public string? PackagePath { get; }
    private Session(ProductConfig product, string? packagePath, string? target, bool uninstall) {
        Product = product; PackagePath = packagePath; Uninstall = uninstall;
        if (packagePath != null) Package = new Package(packagePath);
        Target = target ?? Platform.DefaultDirectory(product);
    }
    public static Session Create(string? packagePath, string? target, bool uninstall) {
        var platform = new PlatformServices();
        if (uninstall) {
            if (target == null) throw new ArgumentException("--dir is required when uninstalling.");
            new Engine(platform).RecoverInterrupted(target);
            return new((Engine.ReadReceipt(target) ?? throw new IOException("No installation receipt found.")).Product, null, target, true);
        }
        if (packagePath == null || !File.Exists(packagePath)) return Preview();
        using var package = new Package(packagePath); return new(package.Manifest.Product, packagePath, target, false);
    }
    public static Session Preview() => new(new() { Id = "org.divinelabs.example", Name = "DivineLabs Installer", Publisher = "DivineLabs", Version = "0.1.0", Language = "en", Description = "Your application. One configuration. Every desktop.", Links = [new("GitHub", "https://github.com/TheDivineLabs/DivineLabs_Installer"), new("Documentation", "https://github.com/TheDivineLabs/DivineLabs_Installer#readme")] }, null, null, false);
    public byte[]? Asset(string? name) {
        if (name == null) return null;
        if (Package != null) return Package.ReadAsset(name);
        string path = Paths.Inside(Target, Paths.StateDirectory + "/branding/" + name); Paths.NoLinks(path);
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }
    public string License => Asset(Product.License) is { } bytes ? Encoding.UTF8.GetString(bytes) : "MIT License\n\nCopyright (c) 2026 DivineLabs\n\nThis installer is free and open-source software.\nhttps://github.com/TheDivineLabs/DivineLabs_Installer";
    public void Dispose() => Package?.Dispose();
}

public static class MaintenanceHelper {
    public static string Root => Path.Combine(Path.GetTempPath(), "DivineLabsInstaller");
    public static bool Relocate(string target, bool silent, bool removeData) {
        string source = AppContext.BaseDirectory;
        string ownedRuntime = Paths.Inside(Path.GetFullPath(target), Paths.RuntimeDirectory);
        if (!Path.TrimEndingDirectorySeparator(source).Equals(Path.TrimEndingDirectorySeparator(ownedRuntime), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) return false;
        Directory.CreateDirectory(Root); Paths.NoLinks(Root);
        string work = Path.Combine(Root, "maintenance-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(work);
        foreach (string file in DivineLabs.Installer.Core.Package.EnumerateFiles(source)) {
            string relative = Path.GetRelativePath(source, file).Replace('\\', '/'); string destination = Paths.Inside(work, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!); File.Copy(file, destination);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(destination, File.GetUnixFileMode(file));
        }
        string marker = Path.Combine(work, ".maintenance-owned"); File.WriteAllText(marker, "DivineLabs Installer");
        var start = new ProcessStartInfo(Path.Combine(work, PlatformServices.HostExecutable)) { UseShellExecute = false, CreateNoWindow = silent };
        foreach (string argument in new[] { "--uninstall", "--dir", target, "--maintenance-helper" }) start.ArgumentList.Add(argument);
        if (silent) start.ArgumentList.Add("--silent"); if (removeData) start.ArgumentList.Add("--remove-data");
        Process.Start(start); return true;
    }
    public static void Cleanup() {
        string work = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
        string prefix = Path.GetFullPath(Root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!work.StartsWith(prefix, StringComparison.Ordinal) || !Path.GetFileName(work).StartsWith("maintenance-", StringComparison.Ordinal) ||
            !File.Exists(Path.Combine(work, ".maintenance-owned"))) return;
        Paths.NoLinks(work);
        if (!OperatingSystem.IsWindows()) { Paths.DeleteOwnedTree(work); return; }
        string Q(string value) => "'" + value.Replace("'", "''") + "'";
        string code = "Wait-Process -Id " + Environment.ProcessId + " -ErrorAction SilentlyContinue;$p=" + Q(work) +
            ";if((Test-Path -LiteralPath $p)-and -not((Get-Item -LiteralPath $p).Attributes -band [IO.FileAttributes]::ReparsePoint)-and -not(Get-ChildItem -LiteralPath $p -Recurse -Force|Where-Object{$_.Attributes -band [IO.FileAttributes]::ReparsePoint})){Remove-Item -LiteralPath $p -Recurse -Force}";
        var start = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true };
        foreach (string argument in new[] { "-NoProfile", "-NonInteractive", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes(code)) }) start.ArgumentList.Add(argument);
        Process.Start(start);
    }
}
