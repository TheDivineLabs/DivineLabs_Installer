using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
namespace DivineLabs.Installer.Examples;
internal static class Program {
    [STAThread] public static int Main(string[] args) {
        LauncherApp.PackagePath = args.ElementAtOrDefault(0) ?? "";
        LauncherApp.TargetDirectory = args.ElementAtOrDefault(1) ?? "";
        return AppBuilder.Configure<LauncherApp>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
    }
}
public sealed class LauncherApp : Application {
    public static string PackagePath { get; set; } = "";
    public static string TargetDirectory { get; set; } = "";
    public override void OnFrameworkInitializationCompleted() {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new LauncherWindow(PackagePath, TargetDirectory);
        base.OnFrameworkInitializationCompleted();
    }
}
