using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CodexDreamSkin.Pages;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    private void AppearanceSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AppearanceSelector.SelectedItem is not ComboBoxItem item ||
            App.Current is not App app ||
            app.MainWindow?.Content is not FrameworkElement root)
        {
            return;
        }

        root.RequestedTheme = item.Tag?.ToString() switch
        {
            "light" => ElementTheme.Light,
            "dark" => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
    }
}
