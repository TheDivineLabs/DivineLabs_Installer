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
    public static readonly StyledProperty<IBrush> CardBrushProperty = AvaloniaProperty.Register<OperationProgressView, IBrush>(nameof(CardBrush), Brushes.White);
    public static readonly StyledProperty<IBrush> LineBrushProperty = AvaloniaProperty.Register<OperationProgressView, IBrush>(nameof(LineBrush), Brush.Parse("#DEE3ED"));
    public static readonly StyledProperty<IBrush> RingTrackBrushProperty = AvaloniaProperty.Register<OperationProgressView, IBrush>(nameof(RingTrackBrush), Brush.Parse("#E2E7F0"));
    public IBrush CardBrush { get => GetValue(CardBrushProperty); set => SetValue(CardBrushProperty, value); }
    public IBrush LineBrush { get => GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }
    public IBrush RingTrackBrush { get => GetValue(RingTrackBrushProperty); set => SetValue(RingTrackBrushProperty, value); }
    private readonly TextBlock title = Label(29), hint = Label(14), detail = Label(13), percent = Label(34);
    private readonly ProgressRing ring = new() { Width = 126, Height = 126 };
    private readonly TextBlock[] labels = [Label(14), Label(14), Label(14)];
    private readonly TextBlock[] states = [Label(14), Label(14), Label(14)];
    private readonly Border stages = new() { CornerRadius = new(9), Padding = new(18, 8), BorderThickness = new(1) };
    /// <summary>Localized labels for preparation, file operations and final verification.</summary>
    public string[] StageLabels { get; set; } = ["Preparation", "Files", "Verification"];
    /// <summary>Localized waiting status.</summary>
    public string WaitingText { get; set; } = "Waiting";
    /// <summary>Localized active status.</summary>
    public string WorkingText { get; set; } = "Working";
    /// <summary>Localized completed status.</summary>
    public string CompleteText { get; set; } = "Complete";
    /// <summary>Refreshes labels after the host changes localization.</summary>
    public void RefreshLabels() => Update();
    private readonly ProgressBar bar = new() { Minimum = 0, Maximum = 100, Height = 6, Margin = new(0, 17, 0, 12) };
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
        title.FontWeight = percent.FontWeight = FontWeight.SemiBold;
        var circle = new Grid { Width = 126, Height = 126, Margin = new(0, 0, 0, 18), HorizontalAlignment = HorizontalAlignment.Center };
        circle.Children.Add(ring); circle.Children.Add(percent);
        hint.Margin = new(0, 9, 0, 18);
        var rows = new StackPanel();
        for (int i = 0; i < 3; i++) {
            var row = new Grid { ColumnDefinitions = new("*,Auto"), Margin = new(0, 9) };
            labels[i].TextAlignment = TextAlignment.Left; states[i].TextAlignment = TextAlignment.Right;
            Grid.SetColumn(states[i], 1); row.Children.Add(labels[i]); row.Children.Add(states[i]); rows.Children.Add(row);
        }
        stages.Child = rows;
        Content = new StackPanel { MaxWidth = 720, HorizontalAlignment = HorizontalAlignment.Stretch,
            Children = { circle, title, hint, stages, bar, detail } }; Update();
    }
    private static TextBlock Label(double size) => new() { FontSize = size, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Stretch };
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) { base.OnPropertyChanged(change); if (bar != null) Update(); }
    private void Update() {
        title.Text = Title; hint.Text = Hint; detail.Text = Detail; bar.Value = Math.Clamp(Percent, 0, 100); bar.IsIndeterminate = IsIndeterminate;
        title.Foreground = percent.Foreground = TextBrush; hint.Foreground = detail.Foreground = MutedBrush; bar.Foreground = AccentBrush; bar.Background = SurfaceBrush;
        ring.Value = Percent; ring.Accent = AccentBrush; ring.Track = RingTrackBrush;
        percent.Text = IsIndeterminate ? "…" : $"{Math.Clamp((int)Percent, 0, 100)}%";
        stages.Background = CardBrush; stages.BorderBrush = LineBrush;
        for (int i = 0; i < 3; i++) {
            labels[i].Text = i < StageLabels.Length ? StageLabels[i] : ""; labels[i].Foreground = TextBrush;
            int state = i == 0 ? (Percent >= 5 ? 2 : 1) : i == 1 ? (Percent >= 94 ? 2 : Percent >= 5 ? 1 : 0) : Percent >= 100 ? 2 : Percent >= 94 ? 1 : 0;
            states[i].Text = state == 2 ? CompleteText : state == 1 ? WorkingText : WaitingText;
            states[i].Foreground = state == 2 ? Brush.Parse("#24855C") : state == 1 ? AccentBrush : Brush.Parse("#8891A2");
        }
    }
}

internal sealed class ProgressRing : Control {
    public static readonly StyledProperty<double> ValueProperty = AvaloniaProperty.Register<ProgressRing, double>(nameof(Value));
    public static readonly StyledProperty<IBrush> AccentProperty = AvaloniaProperty.Register<ProgressRing, IBrush>(nameof(Accent), Brushes.SlateBlue);
    public static readonly StyledProperty<IBrush> TrackProperty = AvaloniaProperty.Register<ProgressRing, IBrush>(nameof(Track), Brushes.LightGray);
    static ProgressRing() => AffectsRender<ProgressRing>(ValueProperty, AccentProperty, TrackProperty);
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public IBrush Accent { get => GetValue(AccentProperty); set => SetValue(AccentProperty, value); }
    public IBrush Track { get => GetValue(TrackProperty); set => SetValue(TrackProperty, value); }
    public override void Render(DrawingContext context) {
        base.Render(context);
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2); double radius = Math.Max(0, Math.Min(Bounds.Width, Bounds.Height) / 2 - 5);
        var pen = new Pen(Accent, 7, lineCap: PenLineCap.Round);
        context.DrawEllipse(null, new Pen(Track, 7), center, radius, radius);
        double value = Math.Clamp(Value, 0, 100); if (value <= 0) return;
        if (value >= 100) { context.DrawEllipse(null, pen, center, radius, radius); return; }
        double angle = value / 100 * Math.PI * 2; var geometry = new StreamGeometry();
        using (var drawing = geometry.Open()) {
            drawing.BeginFigure(new Point(center.X, center.Y - radius), false);
            drawing.ArcTo(new Point(center.X + Math.Sin(angle) * radius, center.Y - Math.Cos(angle) * radius), new Size(radius, radius), 0, value > 50, SweepDirection.Clockwise);
            drawing.EndFigure(false);
        }
        context.DrawGeometry(null, pen, geometry);
    }
}
