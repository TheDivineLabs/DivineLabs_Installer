using Avalonia.Markup.Xaml;
using Avalonia.Styling;
namespace DivineLabs.Installer.Templates.Classic;
/// <summary>Classic styles scoped by ClassicWindow. Does not modify host application resources.</summary>
public sealed partial class ClassicTheme : Styles {
    public ClassicTheme() => AvaloniaXamlLoader.Load(this);
}
