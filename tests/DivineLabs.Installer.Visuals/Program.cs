using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using DivineLabs.Installer.Core;
using DivineLabs.Installer.Desktop;

string output = Path.GetFullPath(args.Length > 0 ? args[0] : "artifacts/screenshots"); Directory.CreateDirectory(output);
AppBuilder.Configure<App>().UseSkia().WithInterFont().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
int count = 0;
void Render(Session session, string prefix, int[] pages) {
    var window = new MainWindow(session); window.Show();
    foreach (string language in new[] { "en", "ru" }) foreach (bool dark in new[] { false, true }) foreach (int page in pages) {
        window.PreviewPage(page, language, dark); Dispatcher.UIThread.RunJobs();
        using var frame = window.CaptureRenderedFrame() ?? throw new Exception("No rendered frame.");
        frame.Save(Path.Combine(output, $"{prefix}{language}-{(dark ? "dark" : "light")}-{page}.png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions()); count++;
    }
    window.Hide();
}
using var install = Session.Preview(); Render(install, "", [0, 1, 2, 3, 4]);
string fixture = Path.Combine(Path.GetTempPath(), "DivineLabs-visual-fixture-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(Path.Combine(fixture, Paths.StateDirectory));
var receipt = new Receipt(install.Product.Id, "0.1.0", RuntimeInformation.RuntimeIdentifier, install.Product, "app", [new("app", new string('A', 64))], false, false, false);
File.WriteAllText(Paths.Inside(fixture, Paths.ReceiptPath), JsonSerializer.Serialize(receipt, InstallerJson.Default.Receipt));
using (var uninstall = Session.Create(null, fixture, true)) Render(uninstall, "uninstall-", [0, 2, 3, 4]);
Paths.DeleteOwnedTree(fixture);
Console.WriteLine($"Rendered {count} Avalonia screenshots: {output}");
