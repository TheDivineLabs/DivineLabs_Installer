using System.Security.Cryptography;
using System.Runtime.Versioning;
using System.Text.Json.Nodes;
using DivineLabs.Installer.Platforms;

[SupportedOSPlatform("windows")]
internal static class WindowsShortcutChecks {
    public static void Run(string parent) {
        string root = Path.Combine(parent, "installer-shortcut-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root); string directory = Path.Combine(root, "owned-shortcuts"), id = "org.divinelabs.shortcut-test";
        string a = Path.Combine(root, "installation-a", "app.exe"), b = Path.Combine(root, "installation-b", "app.exe"), c = Path.Combine(root, "installation-c", "app.exe");
        int count = 0;
        void Check(bool value, string message) { if (!value) throw new Exception(message); count++; Console.WriteLine("PASS: " + message); }
        string[] Links() => Directory.GetFiles(directory, "*.lnk");
        string Hash(string p) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)));
        WindowsShortcuts.Write(directory, id, a, Path.GetDirectoryName(a)!, "Test A");
        string first = Path.Combine(directory, id + ".lnk"); Check(File.Exists(first), "First installation creates its shortcut");
        WindowsShortcuts.Write(directory, id, a, Path.GetDirectoryName(a)!, "Test A update");Check(Links().Length == 1, "Same installation updates its existing shortcut");
        string original = Hash(first);
        WindowsShortcuts.Write(directory, id, b, Path.GetDirectoryName(b)!, "Test B");Check(Links().Length == 2, "Other installation gets an independent shortcut");
        Check(Hash(first) == original, "A shortcut pointing to another installation is unchanged");
        WindowsShortcuts.Write(directory, id, c, Path.GetDirectoryName(c)!, "Test C");Check(Links().Length == 3, "Third installation coexists");
        WindowsShortcuts.Remove(directory, id, b);Check(Links().Length == 2 && File.Exists(first), "Removal only deletes the matching target");
        WindowsShortcuts.Remove(directory, id, a);Check(!File.Exists(first) && Links().Length == 1, "Original installation cleanup preserves the third installation");
        WindowsShortcuts.Write(directory, id, c, Path.GetDirectoryName(c)!, "Test C update");Check(Links().Length == 1, "A free base name does not duplicate an existing own shortcut");
        WindowsShortcuts.Remove(directory, id, c);WindowsShortcuts.Remove(directory, id, c);Check(Links().Length == 0, "Repeated owned removal is safe");
        bool plain = false;try { PlatformServices.PowerShell("Write-Progress -Activity 'test' -Status 'test';throw 'Проверка ошибки'"); }
        catch (IOException e) { plain = e.Message.Contains("Проверка ошибки") && !e.Message.Contains("CLIXML") && !e.Message.Contains("<Objs") && e.Message.Length < 200; }
        Check(plain, "PowerShell errors remain readable UTF-8 without CLIXML");
        File.WriteAllText(Path.Combine(root, "result.json"), new JsonObject { ["passed"] = true, ["assertions"] = count, ["nativeWindowsComShortcuts"] = true, ["actualUserIntegrationsTouched"] = false, ["root"] = root }.ToJsonString());
        Console.WriteLine("Owned shortcut evidence: " + root);
    }
}
