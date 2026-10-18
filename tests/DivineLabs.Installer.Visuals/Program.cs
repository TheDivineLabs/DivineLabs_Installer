using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using DivineLabs.Installer.Core;
using DivineLabs.Installer.Desktop;
using DivineLabs.Installer.Platforms;
using DivineLabs.Installer.Templates.Classic;

string output = Path.GetFullPath(args.Length > 0 ? args[0] : "artifacts/screenshots"); Directory.CreateDirectory(output);
AppBuilder.Configure<App>().UseSkia().WithInterFont().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
int count = 0;
var sentinel = Avalonia.Media.Brush.Parse("#123456"); Application.Current!.Resources["Background"] = sentinel;
void Render(ClassicSession session, string prefix, int[] pages) {
    var window = new ClassicWindow(session); window.Show();
    foreach (string language in new[] { "en", "ru" }) foreach (bool dark in new[] { false, true }) foreach (int page in pages) {
        window.PreviewPage(page, language, dark); Dispatcher.UIThread.RunJobs();
        using var frame = window.CaptureRenderedFrame() ?? throw new Exception("No rendered frame.");
        frame.Save(Path.Combine(output, $"{prefix}{language}-{(dark ? "dark" : "light")}-{page}.png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions()); count++;
    }
    if (!ReferenceEquals(Application.Current!.Resources["Background"], sentinel)) throw new Exception("Classic modified host application resources.");
    window.Hide();
}
var platform = new PlatformServices(); IInstallerEngine engine = new Engine(platform);
using var install = ClassicSession.Preview(engine, platform); Render(install, "", [0, 1, 2, 3, 4]);
string fixture = Path.Combine(Path.GetTempPath(), "DivineLabs-visual-fixture-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(Path.Combine(fixture, Paths.StateDirectory));
var receipt = new Receipt(install.Product.Id, "0.1.0", RuntimeInformation.RuntimeIdentifier, install.Product, "app", [new("app", new string('A', 64))], false, false, false);
File.WriteAllText(Paths.Inside(fixture, Paths.ReceiptPath), JsonSerializer.Serialize(receipt, InstallerJson.Default.Receipt));
using (var uninstall = ClassicSession.OpenInstallationAsync(fixture, engine, platform).GetAwaiter().GetResult()) Render(uninstall, "uninstall-", [0, 2, 3, 4]);
Paths.DeleteOwnedTree(fixture);
var launcher = new DivineLabs.Installer.Examples.LauncherWindow(); launcher.Show(); Dispatcher.UIThread.RunJobs();
using (var frame = launcher.CaptureRenderedFrame() ?? throw new Exception("No launcher frame."))
    frame.Save(Path.Combine(output, "embedded-launcher.png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
launcher.Hide();
Console.WriteLine($"Rendered {count} Avalonia screenshots: {output}");
