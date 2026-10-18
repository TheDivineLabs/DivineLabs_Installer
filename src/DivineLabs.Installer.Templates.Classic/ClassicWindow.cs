using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DivineLabs.Installer.Core;
using DivineLabs.Installer.Components;
using APath = Avalonia.Controls.Shapes.Path;
namespace DivineLabs.Installer.Templates.Classic;

public sealed class ClassicWindow : Window {
    private readonly ClassicSession session;
    private readonly Dictionary<string, Bitmap> images = [];
    private string language, target, status = "", error = "";
    private bool dark, accepted, desktopShortcut = true, menuShortcut = true, removeData, busy, succeeded, allowClose;
    private int page, licenseTab;
    private CancellationTokenSource? cancellation;
    private DirectoryPicker? targetBox;
    private OperationProgressView? operationProgress;
    private Border? overlay;
    private IBrush background = Brushes.White, surface = Brushes.White, line = Brushes.Gray, text = Brushes.Black, muted = Brushes.Gray, white = Brushes.White, accent;
    public int Page => page;
    public ClassicWindow(ClassicSession session) {
        this.session = session; desktopShortcut = session.Options.DesktopShortcut; menuShortcut = session.Options.MenuShortcut; language = session.Product.Language; target = session.Target; accent = Brush.Parse(session.Product.Branding.Accent);
        Title = session.Product.Name; Width = 1100; Height = 790; MinWidth = 940; MinHeight = 690;
        WindowDecorations = WindowDecorations.None; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        if (Image(session.Product.Branding.Icon) is { } icon) Icon = new WindowIcon(icon);
        else { using var stream = typeof(ClassicWindow).Assembly.GetManifestResourceStream("DivineLabs.Installer.Icon")!; Icon = new WindowIcon(stream); }
        Closing += (_, e) => { if (!allowClose && page != 4) { e.Cancel = true; ConfirmCancel(); } };
        Closed += (_, _) => { foreach (var image in images.Values) image.Dispose(); cancellation?.Dispose(); };
        Styles.Add(new ClassicTheme());
        Build();
    }
    private string T(string en, string ru) => language == "ru" ? ru : en;
    private static Grid Grid(string columns = "*", string rows = "*") => new() { ColumnDefinitions = new(columns), RowDefinitions = new(rows) };
    private static TControl Place<TControl>(TControl control, int column = 0, int row = 0) where TControl : Control { Avalonia.Controls.Grid.SetColumn(control, column); Avalonia.Controls.Grid.SetRow(control, row); return control; }
    private TextBlock Text(string value, double size = 14, bool secondary = false, FontWeight? weight = null) => new() {
        Text = value, FontSize = size, Foreground = secondary ? muted : text, FontWeight = weight ?? FontWeight.Normal, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center
    };
    private TextBlock Center(string value, double size, bool secondary = false, FontWeight? weight = null) { var item = Text(value, size, secondary, weight); item.TextAlignment = TextAlignment.Center; item.HorizontalAlignment = HorizontalAlignment.Stretch; return item; }
    private Button Button(string title, Action action, bool primary = false) {
        var button = new Button { Content = title, FontSize = 13 };
        if (primary) button.Classes.Add("primary"); button.Click += (_, _) => action(); return button;
    }
    private Border Card(Control content, double padding = 18) => new() { Child = content, BorderBrush = line, BorderThickness = new(1), CornerRadius = new(10), Padding = new(padding) };
    private Bitmap? Image(string? name) {
        if (name == null) return null;
        if (images.TryGetValue(name, out var existing)) return existing;
        if (session.Asset(name) is not { } data) return null;
        using var stream = new MemoryStream(data); return images[name] = new Bitmap(stream);
    }
    private Control ProductIcon(double size) {
        if (Image(session.Product.Branding.Icon) is { } bitmap) return new Border { Width = size, Height = size, CornerRadius = new(size * .16), ClipToBounds = true, Child = new Image { Source = bitmap, Stretch = Stretch.Uniform } };
        if (session.Product.Branding.ShowFrameworkBranding) return Logo(size);
        var initial = Center(System.Globalization.StringInfo.GetNextTextElement(session.Product.Name), size * .5, weight: FontWeight.Bold); initial.Foreground = Brushes.White;
        return new Border { Width = size, Height = size, CornerRadius = new(size * .2), Background = accent, Child = initial };
    }
    public static Control Logo(double size) {
        var grid = new Grid { Width = 144, Height = 144 };
        grid.Children.Add(new Border { CornerRadius = new(32), Background = new LinearGradientBrush { StartPoint = new(0, 0, RelativeUnit.Relative), EndPoint = new(1, 1, RelativeUnit.Relative), GradientStops = [new(Color.Parse("#7B55FF"), 0), new(Color.Parse("#5347DB"), 1)] } });
        grid.Children.Add(new APath { Fill = Brushes.White, Data = Geometry.Parse("M28,38 C28,22 40,16 54,16 L64,16 C82,16 96,26 104,42 L50,42 C41,42 33,47 28,54 Z M28,66 C28,54 40,48 52,48 L106,48 C109,58 109,67 106,78 L50,78 C41,78 33,83 28,91 Z M28,102 C28,90 40,84 52,84 L103,84 C95,102 79,114 61,114 L49,114 C40,114 33,119 28,124 Z"), RenderTransform = new TranslateTransform(8, -2) });
        return new Viewbox { Width = size, Height = size, Child = grid };
    }
    private void Build() {
        background = Brush.Parse(dark ? "#10141C" : "#FAFBFE"); surface = Brush.Parse(dark ? "#1A2230" : "#F0F2F8");
        line = Brush.Parse(dark ? "#303D50" : "#DEE3ED"); text = Brush.Parse(dark ? "#EEF2F8" : "#1B2638"); muted = Brush.Parse(dark ? "#A4B0C2" : "#667389"); white = Brush.Parse(dark ? "#151D29" : "#FFFFFF");
        RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        foreach (var resource in new[] { ("Background", background), ("Surface", surface), ("Line", line), ("Text", text), ("Muted", muted), ("WhiteSurface", white), ("Accent", accent) })
            Resources[resource.Item1] = resource.Item2;
        Resources["SystemAccentColor"] = Color.Parse(session.Product.Branding.Accent);
        Background = background; targetBox = null;
        var root = Grid(rows: "50,*"); root.Children.Add(Header());
        bool cover = page is 0 or 4;
        var body = Grid(cover ? "330,*" : "0,*");
        if (cover) body.Children.Add(Cover());
        var pane = Grid(rows: "82,14,*,Auto"); pane.Margin = new(34, 24, 34, 20);
        pane.Children.Add(Steps());
        Control content = page switch { 0 => Welcome(), 1 => Licenses(), 2 => Options(), 3 => Progress(), _ => Finish() };
        content.MaxWidth = 900; content.HorizontalAlignment = HorizontalAlignment.Stretch;
        pane.Children.Add(Place(content, row: 2)); pane.Children.Add(Place(Footer(), row: 3));
        body.Children.Add(Place(pane, 1)); root.Children.Add(Place(body, row: 1));
        overlay = new Border { IsVisible = false, Background = Brush.Parse("#880C1020") }; Avalonia.Controls.Grid.SetRowSpan(overlay, 2); root.Children.Add(overlay);
        Content = new Border { Background = background, BorderBrush = line, BorderThickness = new(1), Child = root };
    }
    private Control Header() {
        var header = Grid("*,140,16,140,16,48,48,48"); header.Background = Brush.Parse(dark ? "#151B28" : "#F5F6FC"); header.Margin = new(16, 0, 0, 0);
        var title = Grid("28,12,*"); title.Children.Add(session.Product.Branding.ShowFrameworkBranding ? Logo(28) : ProductIcon(28)); title.Children.Add(Place(Text(session.Product.Name, 16, weight: FontWeight.SemiBold), 2)); header.Children.Add(title);
        var lang = new ComboBox { ItemsSource = new[] { "English", "Русский" }, SelectedIndex = language == "ru" ? 1 : 0, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Stretch };
        lang.SelectionChanged += (_, _) => { string value = lang.SelectedIndex == 1 ? "ru" : "en"; if (value != language) { SaveTarget(); language = value; Build(); } }; header.Children.Add(Place(lang, 1));
        var theme = new ComboBox { ItemsSource = new[] { T("☼  Light", "☼  Светлая"), T("☾  Dark", "☾  Тёмная") }, SelectedIndex = dark ? 1 : 0, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Stretch };
        theme.SelectionChanged += (_, _) => { bool value = theme.SelectedIndex == 1; if (value != dark) { SaveTarget(); dark = value; Build(); } }; header.Children.Add(Place(theme, 3));
        Button Caption(string label, string geometry, Action action, bool rounded = false) {
            var glyph = new APath { Width = 12, Height = 12, Stretch = Stretch.Uniform,
                Data = Geometry.Parse(geometry), Stroke = text, StrokeThickness = 1.2,
                StrokeLineCap = rounded ? PenLineCap.Round : PenLineCap.Flat, StrokeJoin = PenLineJoin.Round };
            var button = Button("", action); button.Content = glyph; button.Classes.Add("chrome");
            Avalonia.Automation.AutomationProperties.SetName(button, label); ToolTip.SetTip(button, label);
            return button;
        }
        var minimize = Caption(T("Minimize", "Свернуть"), "M1,7 L13,7", () => WindowState = WindowState.Minimized);
        minimize.Content = new Border { Width = 12, Height = 1.2, Background = text }; header.Children.Add(Place(minimize, 5));
        var maximize = Caption(T("Maximize", "Развернуть"), "M1,1 H13 V13 H1 Z", () => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized);
        void UpdateMaximize() {
            bool restored = WindowState == WindowState.Maximized;
            ((APath)maximize.Content!).Data = Geometry.Parse(restored ? "M4,1 H13 V10 H10 M1,4 H10 V13 H1 Z" : "M1,1 H13 V13 H1 Z");
            string label = restored ? T("Restore", "Восстановить") : T("Maximize", "Развернуть");
            Avalonia.Automation.AutomationProperties.SetName(maximize, label); ToolTip.SetTip(maximize, label);
        }
        UpdateMaximize();
        // Bind the glyph to this window without retaining headers replaced by language/theme changes.
        maximize.AttachedToVisualTree += (_, _) => PropertyChanged += OnWindowState;
        maximize.DetachedFromVisualTree += (_, _) => PropertyChanged -= OnWindowState;
        void OnWindowState(object? sender, AvaloniaPropertyChangedEventArgs e) { if (e.Property == WindowStateProperty) UpdateMaximize(); }
        header.Children.Add(Place(maximize, 6));
        header.Children.Add(Place(Caption(T("Close", "Закрыть"), "M1,1 L13,13 M13,1 L1,13", ConfirmCancel, rounded: true), 7));
        header.PointerPressed += (_, e) => { if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && e.Source is Control source && !source.GetVisualAncestors().Any(a => a is Avalonia.Controls.Button or ComboBox)) BeginMoveDrag(e); };
        return new Border { BorderBrush = line, BorderThickness = new(0, 0, 0, 1), Child = header };
    }
    private Control Cover() {
        var grid = new Grid { ClipToBounds = true, Background = Brush.Parse("#181927") };
        if (Image(session.Product.Branding.Cover) is { } cover) grid.Children.Add(new Image { Source = cover, Stretch = Stretch.UniformToFill });
        else {
            grid.Background = new LinearGradientBrush { StartPoint = new(0, 0, RelativeUnit.Relative), EndPoint = new(1, 1, RelativeUnit.Relative), GradientStops = [new(Color.Parse("#6455E5"), 0), new(Color.Parse("#17182B"), 1)] };
            var illustration = new StackPanel { Spacing = 28, VerticalAlignment = VerticalAlignment.Center, Margin = new(36, 0, 36, 120) };
            illustration.Children.Add(new Border { Child = session.Product.Branding.ShowFrameworkBranding ? Logo(168) : ProductIcon(168), Opacity = .95, HorizontalAlignment = HorizontalAlignment.Center });
            foreach (double opacity in new[] { .15, .10, .06 }) illustration.Children.Add(new Border { Height = 45, CornerRadius = new(12), Background = Brushes.White, Opacity = opacity, Margin = new(8, 0) });
            grid.Children.Add(illustration);
        }
        grid.Children.Add(new Border { Background = new LinearGradientBrush { GradientStops = [new(Color.Parse("#00151725"), .45), new(Color.Parse("#D0151725"), 1)] } });
        var label = Center(session.Product.Name, 34, weight: FontWeight.Bold); label.Foreground = Brush.Parse("#E6C86E"); label.Margin = new(20, 0, 20, 58); label.VerticalAlignment = VerticalAlignment.Bottom; grid.Children.Add(label); return grid;
    }
    private Control Steps() {
        int[] pages = session.Uninstall ? [0, 2, 3, 4] : [0, 1, 2, 3, 4]; int reached = Array.IndexOf(pages, page);
        string[] titles = session.Uninstall ? [T("Start", "Начало"), T("Options", "Параметры"), T("Uninstall", "Удаление"), T("Done", "Готово")] : [T("Start", "Начало"), T("Licenses", "Лицензии"), T("Options", "Параметры"), T("Install", "Установка"), T("Done", "Готово")];
        return new StepIndicator { Labels = titles, ActiveIndex = reached, AccentBrush = accent,
            TrackBrush = line, PageBrush = background, TextBrush = text, MutedBrush = muted };
    }
    private Control Welcome() {
        var panel = new StackPanel { Width = 660, VerticalAlignment = VerticalAlignment.Center };
        var icon = ProductIcon(132); icon.HorizontalAlignment = HorizontalAlignment.Center; icon.Margin = new(0, 0, 0, 18); panel.Children.Add(icon);
        panel.Children.Add(Center(session.Uninstall ? T("Until next time", "До встречи") : T("Welcome", "Добро пожаловать"), 36, weight: FontWeight.Bold));
        var subtitle = Center(session.Uninstall ? T("Remove the app. Keep your progress.", "Удалим программу. Ваш прогресс останется.") : T("Make yourself at home", "Всё готово для вашей программы"), 22, true); subtitle.Margin = new(0, 3, 0, 14); panel.Children.Add(subtitle);
        var body = Center(session.Uninstall ? T("Your settings and backups are kept by default.", "Настройки и данные по умолчанию сохранятся.") : session.Product.Description, 15, true); body.Margin = new(0, 0, 0, 24); panel.Children.Add(body);
        var cards = Grid("*,16,*"); cards.Children.Add(Feature(T("Ready to use\nA simple guided setup.", "Быстрая установка\nВсё в одном комплекте."), false)); cards.Children.Add(Place(Feature(session.Uninstall ? T("Your choice\nKeep or remove app data.", "Ваш выбор\nСохранить или удалить данные.") : T("Flexible options\nSet it up your way.", "Гибкая настройка\nПод ваши предпочтения."), true), 2)); panel.Children.Add(cards);
        var social = Social(); social.Margin = new(0, 20, 0, 0); panel.Children.Add(social);
        return new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Child = panel };
    }
    private Control Feature(string title, bool settings) {
        var grid = Grid("38,*");
        grid.Children.Add(new Viewbox { Width = 31, Height = 31, Child = new APath { Fill = accent, Data = Geometry.Parse(settings ? "M13,1 L19,1 20,6 24,8 29,6 32,12 28,16 29,20 32,23 28,29 23,27 19,29 18,32 12,32 11,27 7,25 3,27 0,21 4,17 3,12 0,9 4,3 9,5 12,4 Z M10,16 A6,6 0 1 0 22,16 A6,6 0 1 0 10,16" : "M3,3 L25,3 31,9 31,27 3,27 Z M8,10 L26,10 26,13 8,13 Z M8,17 L22,17 22,20 8,20 Z") } });
        var label = Text(title, 15, true); label.Margin = new(14, 0, 0, 0); label.LineHeight = 23; grid.Children.Add(Place(label, 1)); var border = Card(grid, 16); border.MinHeight = 94; return border;
    }
    private Control Social() {
        var grid = Grid(string.Join(",", session.Product.Links.Select(_ => "*")) is { Length: > 0 } definitions ? definitions : "*");
        for (int i = 0; i < session.Product.Links.Length; i++) {
            var link = session.Product.Links[i]; var button = Button(link.Label, () => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(link.Url) { UseShellExecute = true })); button.Background = Brushes.Transparent; button.HorizontalAlignment = HorizontalAlignment.Stretch; button.Margin = new(i == 0 ? 0 : 8, 0, i == session.Product.Links.Length - 1 ? 0 : 8, 0); grid.Children.Add(Place(button, i));
        }
        return grid;
    }
    private Control Licenses() {
        var grid = Grid(rows: "Auto,Auto,*,Auto"); grid.Margin = new(20, 0);
        var title = new StackPanel { Spacing = 9, Margin = new(0, 0, 0, 22) }; title.Children.Add(Center(T("Before we begin", "Перед началом установки"), 28, weight: FontWeight.SemiBold)); title.Children.Add(Center(T("Read the application license.", "Ознакомьтесь с лицензией программы."), 14, true)); grid.Children.Add(title);
        var tabs = new StackPanel { Orientation = Orientation.Horizontal };
        string[] labels = [session.Product.Name, T("Installer", "Установщик"), T("Components", "Компоненты")];
        for (int i = 0; i < labels.Length; i++) {
            int index = i; var tab = Button(labels[i], () => { licenseTab = index; Build(); });
            tab.Background = Brushes.Transparent; tab.CornerRadius = new(0); tab.BorderThickness = new(0, 0, 0, 2); tab.BorderBrush = i == licenseTab ? accent : Brushes.Transparent; tab.Foreground = i == licenseTab ? accent : muted; tabs.Children.Add(tab);
        }
        grid.Children.Add(Place(tabs, row: 1));
        string licenseText = session.License;
        if (licenseTab == 1) { using var stream = typeof(ClassicWindow).Assembly.GetManifestResourceStream("DivineLabs.Installer.LICENSE")!; using var reader = new StreamReader(stream); licenseText = reader.ReadToEnd(); }
        if (licenseTab == 2) {
            string directory = System.IO.Path.Combine(AppContext.BaseDirectory, "licenses");
            licenseText = Directory.Exists(directory) ? string.Join("\n\n", Directory.GetFiles(directory).Order(StringComparer.Ordinal).Select(file => System.IO.Path.GetFileName(file) + "\n\n" + File.ReadAllText(file))) : T("Component notices are included with the published runtime.", "Лицензии компонентов включены в публикуемый runtime.");
        }
        var content = new TextBox { Text = licenseText, IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, FontSize = 12, BorderThickness = new(0), Background = white }; ScrollViewer.SetVerticalScrollBarVisibility(content, Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);
        var box = Card(content, 20); box.Background = white; grid.Children.Add(Place(box, row: 2));
        var check = new CheckBox { Content = T("I accept the license terms", "Я принимаю условия лицензионного соглашения"), IsChecked = accepted, Margin = new(0, 18, 0, 2) }; check.IsCheckedChanged += (_, _) => { accepted = check.IsChecked == true; UpdateFooter(); }; grid.Children.Add(Place(check, row: 3)); return grid;
    }
    private Control Options() {
        var panel = new StackPanel { MaxWidth = 720, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(Center(session.Uninstall ? T("Review before uninstalling", "Проверим перед удалением") : T("Where should we install?", "Куда установить программу?"), 29, weight: FontWeight.SemiBold));
        var subtitle = Center(session.Uninstall ? T("The application will be removed from this folder.", "Будет удалена программа из этой папки.") : T($"Choose a folder for {session.Product.Name}.", $"Выберите папку для {session.Product.Name}."), 14, true); subtitle.Margin = new(0, 10, 0, 28); panel.Children.Add(subtitle);
        var row = Grid("35,18,*"); var folder = Text("▰", 35); folder.Foreground = accent; row.Children.Add(folder);
        targetBox = new DirectoryPicker { DirectoryPath = target, IsReadOnly = session.Uninstall,
            BrowseLabel = T("Browse", "Изменить"), PickerTitle = T("Application folder", "Папка программы") };
        row.Children.Add(Place(targetBox, 2));
        var pathCard = Card(row); pathCard.Background = white; panel.Children.Add(pathCard);
        var hint = Text(session.Uninstall ? T("Only files recorded by this installer will be removed.", "Удаляются только файлы из списка установки.") : T("Existing installations can be updated in place.", "Существующую установку можно обновить в той же папке."), 12, true); hint.Margin = new(4, 12, 4, 22); panel.Children.Add(hint);
        var copies = new StackPanel { Spacing = 8 };
        foreach (var copy in session.FindInstallations()) {
            string folderPath = copy.Folder;
            copies.Children.Add(Button($"{copy.Folder}  ·  {copy.Version}", () => { target = folderPath; if (!session.Uninstall) Build(); }));
        }
        if (copies.Children.Count == 0) copies.Children.Add(Text(T("No other installations found.", "Других установок не найдено."), 12, true));
        panel.Children.Add(new Expander { Header = T("Other installations", "Другие установки программы"), Content = copies, Margin = new(0, 0, 0, 20), HorizontalAlignment = HorizontalAlignment.Stretch });
        if (session.Uninstall) {
            var details = new StackPanel { Spacing = 10 }; details.Children.Add(Text(T("Your progress is kept", "Ваш прогресс сохранится"), 14, weight: FontWeight.SemiBold)); details.Children.Add(Text(T("Settings, logs and unrelated files are preserved by default.", "Настройки, журналы и посторонние файлы по умолчанию сохраняются."), 13, true));
            var check = new CheckBox { Content = T("Remove application data as well", "Полное удаление, включая данные программы"), IsChecked = removeData }; check.IsCheckedChanged += (_, _) => removeData = check.IsChecked == true; details.Children.Add(check); var card = Card(details); card.Background = surface; panel.Children.Add(card);
        } else {
            if (session.Options.Integrations && !OperatingSystem.IsMacOS()) {
                var desktop = new CheckBox { Content = ShortcutLabel(T("Create a desktop shortcut", "Создать ярлык на рабочем столе"), T("Quick access to the application.", "Быстрый доступ к программе.")), IsChecked = desktopShortcut }; desktop.IsCheckedChanged += (_, _) => desktopShortcut = desktop.IsChecked == true; var card = Card(desktop); card.Margin = new(0, 0, 0, 10); panel.Children.Add(card);
                var menu = new CheckBox { Content = ShortcutLabel(T("Add to the applications menu", "Добавить в меню приложений"), T("Find the application in the system menu.", "Находите программу среди приложений.")), IsChecked = menuShortcut }; menu.IsCheckedChanged += (_, _) => menuShortcut = menu.IsChecked == true; panel.Children.Add(Card(menu));
            }
            var retained = Text(T("Your application data stays in place during updates.", "При обновлении данные программы остаются на месте."), 12, true); retained.Margin = new(4, 20, 4, 0); panel.Children.Add(retained);
        }
        if (error.Length > 0) { var label = Text(error, 13); label.Foreground = Brush.Parse("#A32C42"); label.Margin = new(0, 14, 0, 0); panel.Children.Add(label); }
        return new ScrollViewer { Content = panel, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
    }
    private Control ShortcutLabel(string title, string hint) {
        var panel = new StackPanel { Spacing = 5 }; panel.Children.Add(Text(title, 15)); panel.Children.Add(Text(hint, 12, true)); return panel;
    }
    private Control Progress() {
        operationProgress = new OperationProgressView { VerticalAlignment = VerticalAlignment.Center, MaxWidth = 720, HorizontalAlignment = HorizontalAlignment.Stretch,
            Title = session.Uninstall ? T("Removing the application", "Удаляем программу") : T("Installing the application", "Устанавливаем программу"),
            Hint = session.Uninstall ? T("Disconnecting components and removing installed files.", "Отключаем компоненты и удаляем установленные файлы.") : T("Copying and verifying the package files.", "Копируем и проверяем файлы выбранного комплекта."),
            StageLabels = [T("Preparation", "Подготовка"), session.Uninstall ? T("Removing files", "Удаление файлов") : T("Installing files", "Установка файлов"), T("Verification", "Проверка")],
            WaitingText = T("Waiting", "Ожидание"), WorkingText = T("In progress…", "Выполняется…"), CompleteText = T("Done  ✓", "Готово  ✓"), IsIndeterminate = busy,
            Detail = status.Length == 0 ? T("Preparing…", "Подготовка…") : status, AccentBrush = accent, SurfaceBrush = surface, CardBrush = white, LineBrush = line, RingTrackBrush = dark ? line : Brush.Parse("#E2E7F0"), TextBrush = text, MutedBrush = muted };
        operationProgress.RefreshLabels();
        return new ScrollViewer { Content = operationProgress, VerticalContentAlignment = VerticalAlignment.Center, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
    }
    private Control Finish() {
        var panel = new StackPanel { Width = 660, VerticalAlignment = VerticalAlignment.Center, Spacing = 18 };
        var icon = ProductIcon(100); icon.HorizontalAlignment = HorizontalAlignment.Center; panel.Children.Add(icon);
        panel.Children.Add(Center(succeeded ? session.Uninstall ? T("Uninstall complete", "Удаление завершено") : T("You're all set", "Всё готово") : T("Operation stopped", "Операция остановлена"), 36, weight: FontWeight.Bold));
        panel.Children.Add(Center(error.Length > 0 ? error : session.Uninstall ? T("The application is removed. Your data choices were respected.", "Программа удалена с выбранными настройками сохранения данных.") : T($"{session.Product.Name} is ready to use.", $"{session.Product.Name} готова к работе."), 15, true));
        if (succeeded && !session.Uninstall) { var launch = Button(T("Launch application", "Запустить программу"), () => session.Platform.Launch(target, session.Package!.Manifest.EntryPoint), true); launch.HorizontalAlignment = HorizontalAlignment.Center; panel.Children.Add(launch); }
        panel.Children.Add(Social()); return new Viewbox { Child = panel, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly };
    }
    private Button? next;
    private Control Footer() {
        var row = Grid("*,Auto,10,Auto,10,Auto"); row.Margin = new(0, 18, 0, 0);
        row.Children.Add(Text(session.Product.Branding.ShowFrameworkBranding ? T("Built with DivineLabs Installer", "Разработано с DivineLabs Installer") : session.Product.Publisher, 10, true));
        if (page == 4) row.Children.Add(Place(Button(T("Done", "Готово"), Exit, true), 5));
        else {
            row.Children.Add(Place(Button(T("Cancel", "Отмена"), ConfirmCancel), 1));
            if (page is 1 or 2) row.Children.Add(Place(Button(T("Back", "Назад"), () => { SaveTarget(); page = page == 2 && session.Uninstall ? 0 : page - 1; Build(); }), 3));
            if (page != 3) {
                next = Button(page == 2 ? session.Uninstall ? T("Uninstall", "Удалить") : T("Install →", "Установить →") : T("Next →", "Далее →"), Next, true); next.MinWidth = 130; row.Children.Add(Place(next, 5)); UpdateFooter();
            }
        }
        return new Border { BorderBrush = line, BorderThickness = new(0, 1, 0, 0), Margin = new(0, 20, 0, 0), Child = row };
    }
    private void UpdateFooter() { if (next != null) { next.IsEnabled = page != 1 || accepted; } }
    private void SaveTarget() { if (targetBox != null) target = targetBox.DirectoryPath; }
    private void Next() { SaveTarget(); if (page == 0) { page = session.Uninstall ? 2 : 1; Build(); } else if (page == 1 && accepted) { page = 2; Build(); } else if (page == 2) _ = Run(); }
    private async Task Run() {
        if (!session.Uninstall && session.PackagePath == null) { error = T("This is a preview. Build an installer with divinepack to install an application.", "Это предварительный просмотр. Соберите установщик через divinepack."); Build(); return; }
        try { target = Paths.Target(target); } catch (Exception ex) { error = ex.Message; Build(); return; }
        error = ""; busy = true; page = 3; cancellation = new(); Build();
        var progress = new Progress<InstallProgress>(value => { status = value.Detail; if (operationProgress != null) { operationProgress.IsIndeterminate = value.Percent < 0; operationProgress.Percent = value.Percent; operationProgress.Detail = value.Detail; } });
        try {
            var token = cancellation.Token;
            OperationResult result = session.Uninstall
                ? await session.Engine.UninstallAsync(new(target, removeData), progress, token)
                : await session.Engine.InstallAsync(new(session.PackagePath!, target, session.RuntimeDirectory, new(session.Options.Integrations, desktopShortcut, menuShortcut)), progress, token);
            succeeded = true; error = string.Join("\n", result.Warnings);
        } catch (OperationCanceledException) { error = T("Cancelled. File changes were rolled back.", "Операция отменена. Изменения файлов восстановлены."); }
        catch (Exception ex) { error = ex.Message; }
        finally { busy = false; page = 4; Build(); }
    }
    private void ConfirmCancel() {
        if (page == 4) { Exit(); return; }
        var panel = new StackPanel { Spacing = 16, Width = 420 };
        panel.Children.Add(Text(T("Cancel this operation?", "Отменить операцию?"), 24, weight: FontWeight.SemiBold));
        panel.Children.Add(Text(busy ? T("File changes will be rolled back.", "Изменения файлов будут отменены.") : T("The application has not been changed.", "Программа пока не изменена."), 14, true));
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 10 };
        buttons.Children.Add(Button(T("Continue", "Продолжить"), () => overlay!.IsVisible = false)); buttons.Children.Add(Button(T("Cancel", "Отменить"), () => { overlay!.IsVisible = false; if (busy) cancellation?.Cancel(); else Exit(); }, true)); panel.Children.Add(buttons);
        overlay!.Child = new Border { Background = background, CornerRadius = new(14), Padding = new(28), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Child = panel }; overlay.IsVisible = true;
    }
    private void Exit() { allowClose = true; Close(); }
    public void PreviewPage(int value, string? locale = null, bool darkTheme = false) { page = value; if (locale != null) language = locale; dark = darkTheme; succeeded = true; accepted = true; Build(); }
}
