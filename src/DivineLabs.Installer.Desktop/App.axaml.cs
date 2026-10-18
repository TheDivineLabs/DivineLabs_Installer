using Avalonia;
using DivineLabs.Installer.Templates.Classic;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
namespace DivineLabs.Installer.Desktop;
public sealed partial class App : Application {
    public static ClassicSession? StartupSession { get; set; }
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
    public override void OnFrameworkInitializationCompleted() {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new ClassicWindow(StartupSession ?? throw new InvalidOperationException("A Classic session is required."));
        base.OnFrameworkInitializationCompleted();
    }
}
