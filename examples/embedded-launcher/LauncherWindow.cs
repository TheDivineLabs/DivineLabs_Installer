using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Themes.Fluent;
using Avalonia.Styling;
using DivineLabs.Installer.Components;
using DivineLabs.Installer.Core;
using DivineLabs.Installer.Platforms;
namespace DivineLabs.Installer.Examples;

/// <summary>A custom launcher that references Core/Platforms/Components, without Classic or the setup executable.</summary>
public sealed class LauncherWindow : Window {
    private readonly IInstallerEngine engine;
    private readonly TextBox packagePath = new() { PlaceholderText = "Path to application.dlpack" };
    private readonly DirectoryPicker target = new();
    private readonly TextBox license = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 100 };
    private readonly CheckBox accepted = new() { Content = "I accept this application's license terms" };
    private readonly Button inspect = new() { Content = "Inspect package" }, install = new() { Content = "Install / update" },
        uninstall = new() { Content = "Uninstall (keep data)" }, cancel = new() { Content = "Cancel" };
    private readonly StepIndicator steps = new() { Labels = ["Application", "Operation", "Ready"] };
    private readonly OperationProgressView progress = new() { Title = "Ready", Hint = "Your launcher owns this interface." };
    private CancellationTokenSource? cancellation;
    private string? inspectedPackage;
    private bool busy;
    public LauncherWindow(string package = "", string directory = "", IInstallerEngine? engine = null) {
        this.engine = engine ?? new Engine(new PlatformServices());
        Title = "Embedded launcher example"; Width = 820; Height = 850; MinWidth = 620; MinHeight = 700;
        RequestedThemeVariant = ThemeVariant.Light; Background = Brush.Parse("#FAFBFE"); Styles.Add(new FluentTheme());
        var panel = new StackPanel { Margin = new(32), Spacing = 16 };
        panel.Children.Add(new TextBlock { Text = "Your launcher", FontSize = 30, FontWeight = FontWeight.Bold });
        panel.Children.Add(new TextBlock { Text = "Core + reusable controls. No Classic window required.", TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(steps); panel.Children.Add(packagePath); panel.Children.Add(inspect); panel.Children.Add(license); panel.Children.Add(accepted);
        panel.Children.Add(new TextBlock { Text = "Separate application directory" }); panel.Children.Add(target);
        panel.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { install, uninstall, cancel } });
        panel.Children.Add(progress); Content = new ScrollViewer { Content = panel };
        packagePath.Text = package; target.DirectoryPath = directory;
        packagePath.TextChanged += (_, _) => { inspectedPackage = null; accepted.IsChecked = false; license.Text = ""; UpdateButtons(); };
        accepted.IsCheckedChanged += (_, _) => UpdateButtons();
        inspect.Click += (_, _) => Inspect(); install.Click += async (_, _) => await RunAsync(false);
        uninstall.Click += async (_, _) => await RunAsync(true); cancel.Click += (_, _) => cancellation?.Cancel();
        Closing += (_, e) => { if (busy) { e.Cancel = true; cancellation?.Cancel(); progress.Hint = "Wait for rollback to finish before closing."; } };
        UpdateButtons();
    }
    private void Inspect() {
        try {
            using var package = new Package(packagePath.Text ?? "");
            license.Text = package.ReadAsset(package.Manifest.Product.License) is { } data
                ? Encoding.UTF8.GetString(data) : "No product license supplied. Review the publisher's distribution terms.";
            inspectedPackage = packagePath.Text; accepted.IsChecked = false;
            progress.Title = package.Manifest.Product.Name + " " + package.Manifest.Product.Version;
            progress.Detail = "Review the license and select an application directory."; steps.ActiveIndex = 0;
        } catch (Exception error) { inspectedPackage = null; progress.Detail = error.Message; }
        UpdateButtons();
    }
    private async Task RunAsync(bool remove) {
        if (busy || (!remove && (inspectedPackage == null || accepted.IsChecked != true))) return;
        busy = true; cancellation = new(); steps.ActiveIndex = 1; UpdateButtons();
        var reporter = new Progress<InstallProgress>(p => { progress.Detail = p.Detail; progress.Percent = p.Percent; });
        try {
            progress.Title = remove ? "Removing" : "Installing";
            // The launcher owns uninstall UX. Never copy this running launcher's folder as maintenance runtime.
            // OS integrations are an explicit host policy, disabled in this example.
            OperationResult result = remove
                ? await engine.UninstallAsync(new(target.DirectoryPath), reporter, cancellation.Token)
                : await engine.InstallAsync(new(inspectedPackage!, target.DirectoryPath, Options: new(false)), reporter, cancellation.Token);
            progress.Title = "Complete"; progress.Detail = result.Warnings.Count == 0 ? "Operation completed." : string.Join("\n", result.Warnings);
            steps.ActiveIndex = 2;
        } catch (OperationCanceledException) { progress.Title = "Cancelled"; progress.Detail = "Uncommitted file changes were rolled back."; steps.ActiveIndex = 0; }
        catch (Exception error) { progress.Title = "Failed"; progress.Detail = error.Message; steps.ActiveIndex = 0; }
        finally { busy = false; cancellation.Dispose(); cancellation = null; UpdateButtons(); }
    }
    private void UpdateButtons() {
        inspect.IsEnabled = packagePath.IsEnabled = target.IsEnabled = accepted.IsEnabled = !busy;
        install.IsEnabled = !busy && inspectedPackage != null && accepted.IsChecked == true;
        uninstall.IsEnabled = !busy; cancel.IsEnabled = busy;
    }
}
