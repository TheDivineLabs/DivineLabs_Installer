using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using DivineLabs.Installer.Core;
using DivineLabs.Installer.Platforms;
using DivineLabs.Installer.Templates.Classic;
namespace DivineLabs.Installer.Examples;
internal static class Program {
    [STAThread] public static int Main(string[] args) {
        if (args.Length == 0) { Console.Error.WriteLine("Usage: ClassicHost application.dlpack [target-directory]"); return 2; }
        try {
            var platform = new PlatformServices(); IInstallerEngine engine = new Engine(platform);
            // The host owns uninstall UX, with no standalone maintenance runtime copied into the target.
            using var session = ClassicSession.OpenPackage(args[0], engine, platform, args.ElementAtOrDefault(1));
            HostApp.Session = session;
            return AppBuilder.Configure<HostApp>().UsePlatformDetect().WithInterFont().StartWithClassicDesktopLifetime(args);
        } catch (Exception error) { Console.Error.WriteLine(error.Message); return 2; }
    }
}
public sealed class HostApp : Application {
    public static ClassicSession? Session { get; set; }
    public override void OnFrameworkInitializationCompleted() {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new ClassicWindow(Session ?? throw new InvalidOperationException("Session required."));
        base.OnFrameworkInitializationCompleted();
    }
}
