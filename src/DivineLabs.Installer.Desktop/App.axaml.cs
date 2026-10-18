using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
namespace DivineLabs.Installer.Desktop;
public sealed partial class App : Application {
    public static Session? StartupSession { get; set; }
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
    public override void OnFrameworkInitializationCompleted() {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow(StartupSession ?? Session.Preview());
        base.OnFrameworkInitializationCompleted();
    }
}
