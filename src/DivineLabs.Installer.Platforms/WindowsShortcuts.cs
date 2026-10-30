using System.Runtime.Versioning;
using DivineLabs.Installer.Core;
namespace DivineLabs.Installer.Platforms;

/// <summary>Windows .lnk integration with independent ownership for side-by-side installs.</summary>
[SupportedOSPlatform("windows")]
public static class WindowsShortcuts {
    private static string Quote(string value) => "'" + value.Replace("'", "''") + "'";
    private static string Candidates(string directory, string productId) {
        Paths.Relative(productId); Paths.NoLinks(directory);
        // Validate all candidate paths before invoking COM; never replace a link/reparse point.
        for (int i = 1; i <= 100; i++) {
            string candidate = Path.Combine(directory, productId + (i == 1 ? "" : $" ({i})") + ".lnk");
            if (File.Exists(candidate) || Directory.Exists(candidate) || new FileInfo(candidate).LinkTarget != null) Paths.NoLinks(candidate);
        }
        return "$s=New-Object -ComObject WScript.Shell;$d=" + Quote(directory) + ";$id=" + Quote(productId)
            + ";$paths=@(1..100|ForEach-Object{$suffix=if($_ -eq 1){''}else{' ('+$_+')'};Join-Path $d ($id+$suffix+'.lnk')});";
    }
    public static void Write(string directory, string productId, string target, string root, string description) {
        string prefix = Candidates(directory, productId); Directory.CreateDirectory(directory);
        PlatformServices.PowerShell(prefix + "$target=" + Quote(target) + ";$p=$null;"
            // Reuse an existing OWN shortcut before considering an unoccupied base name.
            + "foreach($candidate in $paths){if((Test-Path -LiteralPath $candidate)-and $s.CreateShortcut($candidate).TargetPath -eq $target){$p=$candidate;break}};"
            + "if(-not $p){foreach($candidate in $paths){if(-not(Test-Path -LiteralPath $candidate)){$p=$candidate;break}}};"
            + "if(-not $p){throw 'No free shortcut name is available for this application'};"
            + "$l=$s.CreateShortcut($p);$l.TargetPath=$target;$l.WorkingDirectory=" + Quote(root) + ";$l.Description=" + Quote(description) + ";$l.Save()");
    }
    public static void Remove(string directory, string productId, string target) {
        if (!Directory.Exists(directory)) return;
        PlatformServices.PowerShell(Candidates(directory, productId) + "$target=" + Quote(target)
            + ";foreach($p in $paths){if((Test-Path -LiteralPath $p)-and $s.CreateShortcut($p).TargetPath -eq $target){Remove-Item -LiteralPath $p -Force}};");
    }
}
