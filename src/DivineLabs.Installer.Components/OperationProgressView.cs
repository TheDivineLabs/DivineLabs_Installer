using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
namespace DivineLabs.Installer.Components;

/// <summary>Bindable progress display. Hosts marshal progress to the UI thread and own cancellation.</summary>
public sealed class OperationProgressView : UserControl {
    public static readonly StyledProperty<string> TitleProperty = AvaloniaProperty.Register<OperationProgressView, string>(nameof(Title), "");
    public static readonly StyledProperty<string> HintProperty = AvaloniaProperty.Register<OperationProgressView, string>(nameof(Hint), "");
    public static readonly StyledProperty<string> DetailProperty = AvaloniaProperty.Register<OperationProgressView, string>(nameof(Detail), "");
    public static readonly StyledProperty<double> PercentProperty = AvaloniaProperty.Register<OperationProgressView, double>(nameof(Percent));
    public static readonly StyledProperty<bool> IsIndeterminateProperty = AvaloniaProperty.Register<OperationProgressView, bool>(nameof(IsIndeterminate));
    public static readonly StyledProperty<IBrush> AccentBrushProperty = AvaloniaProperty.Register<OperationProgressView, IBrush>(nameof(AccentBrush), Brush.Parse("#6658E8"));
    public static readonly StyledProperty<IBrush> SurfaceBrushProperty = AvaloniaProperty.Register<OperationProgressView, IBrush>(nameof(SurfaceBrush), Brush.Parse("#F0F2F8"));
    public static readonly StyledProperty<IBrush> TextBrushProperty = AvaloniaProperty.Register<OperationProgressView, IBrush>(nameof(TextBrush), Brush.Parse("#1B2638"));
    public static readonly StyledProperty<IBrush> MutedBrushProperty = AvaloniaProperty.Register<OperationProgressView, IBrush>(nameof(MutedBrush), Brush.Parse("#667389"));
    private readonly TextBlock title = Label(30), hint = Label(14), detail = Label(13);
    private readonly ProgressBar bar = new() { Minimum = 0, Maximum = 100, Height = 8, Margin = new(0, 16, 0, 0) };
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Hint { get => GetValue(HintProperty); set => SetValue(HintProperty, value); }
    public string Detail { get => GetValue(DetailProperty); set => SetValue(DetailProperty, value); }
    public double Percent { get => GetValue(PercentProperty); set => SetValue(PercentProperty, value); }
    public bool IsIndeterminate { get => GetValue(IsIndeterminateProperty); set => SetValue(IsIndeterminateProperty, value); }
    public IBrush AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }
    public IBrush SurfaceBrush { get => GetValue(SurfaceBrushProperty); set => SetValue(SurfaceBrushProperty, value); }
    public IBrush TextBrush { get => GetValue(TextBrushProperty); set => SetValue(TextBrushProperty, value); }
    public IBrush MutedBrush { get => GetValue(MutedBrushProperty); set => SetValue(MutedBrushProperty, value); }
    public OperationProgressView() {
        title.FontWeight = FontWeight.SemiBold;
        Content = new StackPanel { Spacing = 18, Children = { title, hint, bar, detail } }; Update();
    }
    private static TextBlock Label(double size) => new() { FontSize = size, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Stretch };
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) { base.OnPropertyChanged(change); if (bar != null) Update(); }
    private void Update() {
        title.Text = Title; hint.Text = Hint; detail.Text = Detail; bar.Value = Math.Clamp(Percent, 0, 100); bar.IsIndeterminate = IsIndeterminate;
        title.Foreground = TextBrush; hint.Foreground = detail.Foreground = MutedBrush; bar.Foreground = AccentBrush; bar.Background = SurfaceBrush;
    }
}
