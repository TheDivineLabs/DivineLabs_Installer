using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using DivineLabs.Installer.Core;
using Microsoft.Win32;
namespace DivineLabs.Installer.Platforms;

public sealed class PlatformServices(string? dataHomeOverride = null, string? desktopOverride = null) : IPlatformServices {
    public static string HostExecutable => OperatingSystem.IsWindows() ? "DivineLabs.Installer.exe" : "DivineLabs.Installer";
    private static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private string DataHome => dataHomeOverride ?? ( Environment.GetEnvironmentVariable("XDG_DATA_HOME") is { Length: > 0 } path && Path.IsPathRooted(path) ? path : Path.Combine(Home, ".local", "share"));
    private string DesktopDirectory => desktopOverride ?? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    public string DefaultDirectory(ProductConfig product) {
        Paths.Validate(product);
        string folder = product.InstallDirectoryName.Length == 0 ? product.Name : product.InstallDirectoryName;
        if (OperatingSystem.IsWindows()) return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", product.Publisher, folder);
        if (OperatingSystem.IsMacOS()) return Path.Combine(Home, "Applications", product.Publisher, folder);
        return Path.Combine(DataHome, product.Publisher, folder);
    }
    public IEnumerable<(string Folder, string Version)> FindInstallations(ProductConfig product) {
        var paths = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        string standard = DefaultDirectory(product); paths.Add(standard);
        string? parent = Path.GetDirectoryName(standard);
        if (parent != null && Directory.Exists(parent)) {
            try { foreach (string child in Directory.EnumerateDirectories(parent).Take(200)) paths.Add(child); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        if (OperatingSystem.IsWindows()) {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + product.Id);
            if (key?.GetValue("InstallLocation") is string path) paths.Add(path);
        }
        foreach (string path in paths) {
            Receipt? receipt = null;
            try { receipt = Engine.ReadReceipt(path); } catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidDataException or ArgumentException) { }
            if (receipt?.ProductId == product.Id) yield return (path, receipt.Version);
        }
    }
    public void Register(string root, Receipt receipt) {
        if (OperatingSystem.IsWindows()) RegisterWindows(root, receipt);
        else if (OperatingSystem.IsLinux()) {
            string exe = Paths.Inside(root, receipt.EntryPoint), host = Paths.Inside(root, Paths.RuntimeDirectory + "/" + HostExecutable);
            string content = $"[Desktop Entry]\nType=Application\nName={DesktopValue(receipt.Product.Name)}\nExec={Exec(exe)}\nPath={DesktopValue(root)}\nTerminal=false\nCategories=Utility;\nX-DivineLabs-Product={receipt.ProductId}\nX-DivineLabs-InstallPath={DesktopValue(root)}\n";
            if (File.Exists(host)) content += $"Actions=Uninstall;\n\n[Desktop Action Uninstall]\nName=Uninstall\nExec={Exec(host)} --uninstall --dir {Exec(root)}\n";
            void WriteLauncher(string path, bool executable) {
                Paths.NoLinks(path); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                if (File.Exists(path) && !File.ReadAllText(path).Contains("X-DivineLabs-InstallPath=" + DesktopValue(root) + "\n", StringComparison.Ordinal)) throw new IOException("Launcher belongs to another installation.");
                File.WriteAllText(path, content);
                if (executable) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
            if (receipt.MenuShortcut) WriteLauncher(Path.Combine(DataHome, "applications", receipt.ProductId + ".desktop"), false);
            if (receipt.DesktopShortcut && Directory.Exists(DesktopDirectory)) WriteLauncher(Path.Combine(DesktopDirectory, receipt.ProductId + ".desktop"), true);
        }
        // macOS applications live in ~/Applications; launch and maintenance need no registry or shell scripts.
    }
    public void Unregister(string root, Receipt receipt) {
        if (OperatingSystem.IsWindows()) UnregisterWindows(root, receipt);
        else if (OperatingSystem.IsLinux()) {
            foreach (string directory in new[] { Path.Combine(DataHome, "applications"), DesktopDirectory }) {
                if (string.IsNullOrEmpty(directory)) continue;
                string path = Path.Combine(directory, receipt.ProductId + ".desktop"); Paths.NoLinks(path);
                if (File.Exists(path) && File.ReadAllText(path).Contains("X-DivineLabs-InstallPath=" + DesktopValue(root) + "\n", StringComparison.Ordinal)) File.Delete(path);
            }
        }
    }
    private static string DesktopValue(string value) => value.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\r", "\\r");
    private static string Exec(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("`", "\\`").Replace("$", "\\$").Replace("%", "%%") + "\"";
    private static string Quote(string value) => "'" + value.Replace("'", "''") + "'";
    [SupportedOSPlatform("windows")]
    private static void RegisterWindows(string root, Receipt receipt) {
        string host = Paths.Inside(root, Paths.RuntimeDirectory + "/" + HostExecutable);
        if (!File.Exists(host)) throw new IOException("Maintenance runtime is missing; Windows integration skipped.");
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + receipt.ProductId);
        key.SetValue("DisplayName", receipt.Product.Name); key.SetValue("DisplayVersion", receipt.Version); key.SetValue("Publisher", receipt.Product.Publisher);
        key.SetValue("InstallLocation", root); key.SetValue("DisplayIcon", host);
        key.SetValue("UninstallString", $"\"{host}\" --uninstall --dir \"{root}\"");
        key.SetValue("QuietUninstallString", $"\"{host}\" --uninstall --silent --dir \"{root}\""); key.SetValue("NoModify", 1); key.SetValue("NoRepair", 1);
        string target = Paths.Inside(root, receipt.EntryPoint);
        foreach (string directory in ShortcutDirectories(receipt)) {
            Directory.CreateDirectory(directory); string path = Path.Combine(directory, receipt.ProductId + ".lnk"); Paths.NoLinks(path);
            PowerShell("$s=New-Object -ComObject WScript.Shell;$p=" + Quote(path) + ";$l=$s.CreateShortcut($p);if((Test-Path -LiteralPath $p)-and $l.TargetPath -ne " + Quote(target) + "){throw 'Shortcut belongs to another installation'};$l.TargetPath=" + Quote(target) + ";$l.WorkingDirectory=" + Quote(root) + ";$l.Description=" + Quote(receipt.Product.Name) + ";$l.Save()");
        }
    }
    [SupportedOSPlatform("windows")]
    private static void UnregisterWindows(string root, Receipt receipt) {
        string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + receipt.ProductId;
        using var key = Registry.CurrentUser.OpenSubKey(keyPath);
        bool ownKey = string.Equals(key?.GetValue("InstallLocation") as string, root, StringComparison.OrdinalIgnoreCase);
        string target = Paths.Inside(root, receipt.EntryPoint);
        foreach (string directory in ShortcutDirectories(receipt)) {
            string path = Path.Combine(directory, receipt.ProductId + ".lnk"); Paths.NoLinks(path);
            if (File.Exists(path)) PowerShell("$s=New-Object -ComObject WScript.Shell;$p=" + Quote(path) + ";if($s.CreateShortcut($p).TargetPath -eq " + Quote(target) + "){Remove-Item -LiteralPath $p -Force}");
        }
        key?.Dispose(); if (ownKey) Registry.CurrentUser.DeleteSubKeyTree(keyPath, false);
    }
    [SupportedOSPlatform("windows")]
    private static IEnumerable<string> ShortcutDirectories(Receipt receipt) {
        if (receipt.DesktopShortcut) yield return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (receipt.MenuShortcut) yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), receipt.Product.Publisher);
    }
    [SupportedOSPlatform("windows")]
    public static void PowerShell(string code) {
        var start = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
        start.ArgumentList.Add("-NoProfile"); start.ArgumentList.Add("-NonInteractive"); start.ArgumentList.Add("-EncodedCommand"); start.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes("$ErrorActionPreference='Stop';" + code)));
        using var process = Process.Start(start) ?? throw new IOException("Cannot start Windows shell integration.");
        string errors = process.StandardError.ReadToEnd(); process.WaitForExit();
        if (process.ExitCode != 0) throw new IOException("Windows integration failed: " + errors.Trim());
    }
    public void Launch(string root, string entryPoint) {
        string path = Paths.Inside(root, entryPoint); Paths.NoLinks(path);
        Process.Start(new ProcessStartInfo(path) { WorkingDirectory = root, UseShellExecute = OperatingSystem.IsWindows() });
    }
}
