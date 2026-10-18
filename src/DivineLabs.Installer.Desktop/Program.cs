using Avalonia;
using DivineLabs.Installer.Platforms;
using DivineLabs.Installer.Templates.Classic;
using DivineLabs.Installer.Core;
namespace DivineLabs.Installer.Desktop;
internal static class Program {
    [STAThread] public static int Main(string[] args) {
        bool helper = args.Contains("--maintenance-helper");
        try {
            if (args.Contains("--help")) { Console.WriteLine("DivineLabs Installer: [--package application.dlpack] [--dir PATH] [--uninstall] [--silent] [--no-integrations] [--remove-data]"); return 0; }
            string? Value(string flag) { int index = Array.IndexOf(args, flag); return index < 0 ? null : index + 1 < args.Length ? args[index + 1] : throw new ArgumentException($"Missing value for {flag}"); }
            string? target = Value("--dir"); bool uninstall = args.Contains("--uninstall"), silent = args.Contains("--silent");
            if (uninstall && target == null) throw new ArgumentException("Uninstall requires --dir.");
            if (uninstall && !helper && MaintenanceHelper.Relocate(target!, silent, args.Contains("--remove-data"))) return 0;
            string package = Value("--package") ?? Path.Combine(AppContext.BaseDirectory, Package.FileName);
            var platform = new PlatformServices(); IInstallerEngine engine = new Engine(platform);
            using var session = uninstall
                ? ClassicSession.OpenInstallationAsync(target!, engine, platform).GetAwaiter().GetResult()
                : File.Exists(package) ? ClassicSession.OpenPackage(package, engine, platform, target, AppContext.BaseDirectory, new())
                : ClassicSession.Preview(engine, platform);
            App.StartupSession = session;
            if (silent) {
                if (uninstall) engine.UninstallAsync(new(session.Target, args.Contains("--remove-data"))).GetAwaiter().GetResult();
                else { if (session.Package == null) throw new IOException("application.dlpack is missing."); engine.InstallAsync(new(package, session.Target, AppContext.BaseDirectory, new(!args.Contains("--no-integrations")))).GetAwaiter().GetResult(); }
                return 0;
            }
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        } catch (Exception error) { Console.Error.WriteLine(error.Message); return 2; }
        finally { if (helper) { try { MaintenanceHelper.Cleanup(); } catch (IOException) { } } }
    }
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont();
}
