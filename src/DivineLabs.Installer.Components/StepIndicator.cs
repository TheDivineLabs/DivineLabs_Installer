using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
namespace DivineLabs.Installer.Components;

/// <summary>Read-only wizard progress. Hosts own navigation and supply localized labels.</summary>
public sealed class StepIndicator : UserControl {
    public static readonly StyledProperty<string[]> LabelsProperty = AvaloniaProperty.Register<StepIndicator, string[]>(nameof(Labels), []);
    public static readonly StyledProperty<int> ActiveIndexProperty = AvaloniaProperty.Register<StepIndicator, int>(nameof(ActiveIndex));
    public static readonly StyledProperty<IBrush> AccentBrushProperty = AvaloniaProperty.Register<StepIndicator, IBrush>(nameof(AccentBrush), Brush.Parse("#6658E8"));
    public static readonly StyledProperty<IBrush> TrackBrushProperty = AvaloniaProperty.Register<StepIndicator, IBrush>(nameof(TrackBrush), Brush.Parse("#DEE3ED"));
    public static readonly StyledProperty<IBrush> PageBrushProperty = AvaloniaProperty.Register<StepIndicator, IBrush>(nameof(PageBrush), Brush.Parse("#FAFBFE"));
    public static readonly StyledProperty<IBrush> TextBrushProperty = AvaloniaProperty.Register<StepIndicator, IBrush>(nameof(TextBrush), Brush.Parse("#1B2638"));
    public static readonly StyledProperty<IBrush> MutedBrushProperty = AvaloniaProperty.Register<StepIndicator, IBrush>(nameof(MutedBrush), Brush.Parse("#667389"));
    public string[] Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }
    public int ActiveIndex { get => GetValue(ActiveIndexProperty); set => SetValue(ActiveIndexProperty, value); }
    public IBrush AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }
    public IBrush TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }
    public IBrush PageBrush { get => GetValue(PageBrushProperty); set => SetValue(PageBrushProperty, value); }
    public IBrush TextBrush { get => GetValue(TextBrushProperty); set => SetValue(TextBrushProperty, value); }
    public IBrush MutedBrush { get => GetValue(MutedBrushProperty); set => SetValue(MutedBrushProperty, value); }
    public StepIndicator() => Rebuild();
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
        base.OnPropertyChanged(change);
        if (change.Property == LabelsProperty || change.Property == ActiveIndexProperty || change.Property == AccentBrushProperty ||
            change.Property == TrackBrushProperty || change.Property == PageBrushProperty || change.Property == TextBrushProperty || change.Property == MutedBrushProperty) Rebuild();
    }
    private void Rebuild() {
        var root = new Grid { MaxWidth = 900 };
        var labels = Labels ?? []; int count = labels.Length;
        if (count == 0) { Content = root; return; }
        int reached = Math.Clamp(ActiveIndex, 0, count - 1);
        var track = new Border { Height = 2, Background = TrackBrush, VerticalAlignment = VerticalAlignment.Top, Margin = new(64, 19, 64, 0) };
        var active = new Border { Height = 2, Background = AccentBrush, VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left, Margin = new(64, 19, 0, 0) };
        root.Children.Add(track); root.Children.Add(active);
        var cells = new Grid { ColumnDefinitions = new(string.Join(",", labels.Select(_ => "*"))) };
        for (int i = 0; i < count; i++) {
            var column = new StackPanel { Spacing = 10, HorizontalAlignment = HorizontalAlignment.Center };
            var number = new TextBlock { Text = i < reached ? "✓" : (i + 1).ToString(), FontSize = 16, FontWeight = FontWeight.SemiBold,
                Foreground = i <= reached ? Brushes.White : MutedBrush, TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Stretch };
            column.Children.Add(new Border { Width = 38, Height = 38, CornerRadius = new(19), Background = i <= reached ? AccentBrush : PageBrush,
                BorderBrush = TrackBrush, BorderThickness = new(i <= reached ? 0 : 1), Child = number });
            column.Children.Add(new TextBlock { Text = labels[i], FontSize = 14, TextWrapping = TextWrapping.Wrap,
                Foreground = i == reached ? TextBrush : MutedBrush, TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Stretch });
            Grid.SetColumn(column, i); cells.Children.Add(column);
        }
        root.Children.Add(cells);
        root.SizeChanged += (_, _) => {
            double edge = root.Bounds.Width / (2 * count);
            track.Margin = new(edge, 19, edge, 0); active.Margin = new(edge, 19, 0, 0);
            active.Width = count == 1 ? 0 : Math.Max(0, (root.Bounds.Width - edge * 2) * reached / (count - 1));
        };
        Content = root;
    }
}
