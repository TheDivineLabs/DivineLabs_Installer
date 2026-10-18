using System.Diagnostics;
using System.Text;
using DivineLabs.Installer.Core;
using DivineLabs.Installer.Platforms;
namespace DivineLabs.Installer.Desktop;

public static class MaintenanceHelper {
    public static string Root => Path.Combine(Path.GetTempPath(), "DivineLabsInstaller");
    public static bool Relocate(string target, bool silent, bool removeData) {
        string source = Paths.CanonicalHostPath(AppContext.BaseDirectory);
        string ownedRuntime = Paths.Inside(Paths.CanonicalHostPath(target), Paths.RuntimeDirectory);
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
        string work = Path.TrimEndingDirectorySeparator(Paths.CanonicalHostPath(AppContext.BaseDirectory));
        string prefix = Paths.CanonicalHostPath(Root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
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
