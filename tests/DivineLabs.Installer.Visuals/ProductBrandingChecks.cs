using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DivineLabs.Installer.Core;
using DivineLabs.Installer.Platforms;
using DivineLabs.Installer.Templates.Classic;

internal static class ProductBrandingChecks {
    public static void Render(string configPath, string licensePath, string output) {
        string root = Path.Combine(Path.GetTempPath(), "installer-branding-" + Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        string configDirectory = Path.GetDirectoryName(Path.GetFullPath(configPath))!;
        var product = JsonSerializer.Deserialize(File.ReadAllText(configPath), InstallerJson.Default.ProductConfig)!;
        string expected = product.Branding.FooterText;if (string.IsNullOrWhiteSpace(expected)) throw new Exception("Custom footer is required for this check.");
        product.Branding.Icon = product.Branding.Icon is { } icon ? Path.GetFullPath(Path.Combine(configDirectory, icon)) : null;
        product.Branding.Cover = product.Branding.Cover is { } cover ? Path.GetFullPath(Path.Combine(configDirectory, cover)) : null;
        product.License = Path.GetFullPath(licensePath);
        string source = Path.Combine(root, "source");Directory.CreateDirectory(source);File.WriteAllText(Path.Combine(source, "owned-fixture.txt"), "No application is executed by this render.");
        string rid = RuntimeInformation.RuntimeIdentifier;
        product.Applications = new() { [rid] = new() { Source = source, EntryPoint = "owned-fixture.txt" } };
        string config = Path.Combine(root, "config.json");File.WriteAllText(config, JsonSerializer.Serialize(product, InstallerJson.Default.ProductConfig));
        string packagePath = Path.Combine(root, "fixture.dlpack");Package.Build(config, rid, packagePath);
        var platform = new PlatformServices();IInstallerEngine engine = new Engine(platform);int frames = 0;
        void Capture(ClassicSession session, string prefix, params int[] pages) {
            var window = new ClassicWindow(session);window.Show();
            foreach (bool dark in new[] { false, true }) foreach (int page in pages) {
                window.PreviewPage(page, "ru", dark);Dispatcher.UIThread.RunJobs();
                var texts = window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToArray();
                if (!texts.Contains(expected) || texts.Any(t => t?.Contains("Разработано с DivineLabs Installer") == true)) throw new Exception("Configured product footer is not rendered.");
                using var frame = window.CaptureRenderedFrame() ?? throw new Exception("No rendered frame.");frame.Save(Path.Combine(output, $"{prefix}-{(dark ? "dark" : "light")}-{page}.png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());frames++;
            }
            window.Close();
        }
        using (var session = ClassicSession.OpenPackage(packagePath, engine, platform)) Capture(session, "install", 1, 4);
        using (var package = new Package(packagePath)) {
            string target = Path.Combine(root, "owned-installation");Directory.CreateDirectory(Path.Combine(target, Paths.StateDirectory));
            var manifest = package.Manifest;var receipt = new Receipt(manifest.Product.Id, manifest.Product.Version, rid, manifest.Product, manifest.EntryPoint, [new(manifest.EntryPoint, manifest.Files.Single(f => f.Path == manifest.EntryPoint).Sha256)], false, false, false);
            File.WriteAllText(Paths.Inside(target, Paths.ReceiptPath), JsonSerializer.Serialize(receipt, InstallerJson.Default.Receipt));
            foreach (var asset in manifest.Assets) {string destination = Paths.Inside(target, Paths.StateDirectory + "/branding/" + asset.Path);Directory.CreateDirectory(Path.GetDirectoryName(destination)!);File.WriteAllBytes(destination, package.ReadAsset(asset.Path)!);}
            using var session = ClassicSession.OpenInstallationAsync(target, engine, platform).GetAwaiter().GetResult();Capture(session, "uninstall", 4);
        }
        Console.WriteLine($"PASS: {frames} product install/license/uninstall renders with the configured footer. No app or user integration started.");
    }
}
