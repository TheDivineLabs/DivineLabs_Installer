using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
namespace DivineLabs.Installer.Components;

/// <summary>Editable local directory with a native folder picker. Does not validate or create an installation target.</summary>
public sealed class DirectoryPicker : UserControl {
    public static readonly StyledProperty<string> DirectoryPathProperty = AvaloniaProperty.Register<DirectoryPicker, string>(nameof(DirectoryPath), "", defaultBindingMode: BindingMode.TwoWay);
    public static readonly StyledProperty<bool> IsReadOnlyProperty = AvaloniaProperty.Register<DirectoryPicker, bool>(nameof(IsReadOnly));
    public static readonly StyledProperty<string> BrowseLabelProperty = AvaloniaProperty.Register<DirectoryPicker, string>(nameof(BrowseLabel), "Browse");
    public static readonly StyledProperty<string> PickerTitleProperty = AvaloniaProperty.Register<DirectoryPicker, string>(nameof(PickerTitle), "Application folder");
    private readonly TextBox editor = new() { FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button browse = new() { FontSize = 13 };
    public event EventHandler<Exception>? BrowseFailed;
    public string DirectoryPath { get => GetValue(DirectoryPathProperty); set => SetValue(DirectoryPathProperty, value); }
    public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }
    public string BrowseLabel { get => GetValue(BrowseLabelProperty); set => SetValue(BrowseLabelProperty, value); }
    public string PickerTitle { get => GetValue(PickerTitleProperty); set => SetValue(PickerTitleProperty, value); }
    public DirectoryPicker() {
        var row = new Grid { ColumnDefinitions = new("*,14,Auto") };
        row.Children.Add(editor); Grid.SetColumn(browse, 2); row.Children.Add(browse); Content = row;
        editor.TextChanged += (_, _) => SetCurrentValue(DirectoryPathProperty, editor.Text ?? "");
        browse.Click += async (_, _) => {
            try { await BrowseAsync(); }
            catch (Exception error) { ToolTip.SetTip(browse, error.Message); BrowseFailed?.Invoke(this, error); }
        };
        Update();
    }
    /// <summary>Opens the host top-level's picker. A detached control has no storage provider.</summary>
    public async Task BrowseAsync() {
        if (IsReadOnly) return;
        var provider = TopLevel.GetTopLevel(this)?.StorageProvider ?? throw new InvalidOperationException("Attach the picker to a window first.");
        var selected = await provider.OpenFolderPickerAsync(new() { Title = PickerTitle, AllowMultiple = false });
        if (selected.Count > 0 && selected[0].Path.IsFile) SetCurrentValue(DirectoryPathProperty, selected[0].Path.LocalPath);
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) { base.OnPropertyChanged(change); if (editor != null && (change.Property == DirectoryPathProperty || change.Property == IsReadOnlyProperty || change.Property == BrowseLabelProperty)) Update(); }
    private void Update() {
        if (editor.Text != DirectoryPath) editor.Text = DirectoryPath;
        editor.IsReadOnly = IsReadOnly; browse.IsVisible = !IsReadOnly; browse.Content = BrowseLabel;
        ((Grid)Content!).ColumnDefinitions = new(IsReadOnly ? "*,0,0" : "*,14,Auto");
    }
}
