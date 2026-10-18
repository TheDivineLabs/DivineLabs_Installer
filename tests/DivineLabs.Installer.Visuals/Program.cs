using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using DivineLabs.Installer.Desktop;

string output = Path.GetFullPath(args.Length > 0 ? args[0] : "artifacts/screenshots"); Directory.CreateDirectory(output);
AppBuilder.Configure<App>().UseSkia().WithInterFont().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
using var session = Session.Preview();
var window = new MainWindow(session); window.Show();
foreach (string language in new[] { "en", "ru" }) foreach (bool dark in new[] { false, true }) foreach (int page in new[] { 0, 1, 2, 3, 4 }) {
    window.PreviewPage(page, language, dark); Dispatcher.UIThread.RunJobs();
    using var frame = window.CaptureRenderedFrame() ?? throw new Exception("No rendered frame.");
    string file = Path.Combine(output, $"{language}-{(dark ? "dark" : "light")}-{page}.png"); frame.Save(file, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
}
Console.WriteLine("Rendered 20 Avalonia screenshots: " + output);
