using CodexDreamSkin.Models;
using CodexDreamSkin.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace CodexDreamSkin.Pages;

public sealed partial class ThemesPage : Page
{
    private readonly ThemeCatalogService _catalog;
    private readonly CodexThemeEngine _engine;
    private ThemeDefinition? _selectedTheme;
    private bool _isLoadingEditor;

    public ThemesPage()
    {
        InitializeComponent();
        var app = (App)Application.Current;
        _catalog = app.ThemeCatalog;
        _engine = app.ThemeEngine;
    }

    private async void ThemesPage_Loaded(object sender, RoutedEventArgs e) => await ReloadThemesAsync();

    private async Task ReloadThemesAsync(string? selectId = null)
    {
        try
        {
            var themes = await _catalog.GetThemesAsync();
            ThemesList.ItemsSource = themes;
            ThemesList.SelectedItem = themes.FirstOrDefault(theme => theme.Id == selectId) ?? themes.FirstOrDefault(theme => theme.IsActive) ?? themes.FirstOrDefault();
        }
        catch (Exception error) { ShowMessage("主题库加载失败", error.Message, InfoBarSeverity.Error); }
    }

    private async void ThemesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedTheme = ThemesList.SelectedItem as ThemeDefinition;
        if (_selectedTheme is null) return;
        _isLoadingEditor = true;
        try
        {
            NameTextBox.Text = _selectedTheme.Name;
            SelectTag(AppearanceComboBox, _selectedTheme.Appearance);
            SelectTag(SafeAreaComboBox, _selectedTheme.SafeArea);
            SelectTag(TaskModeComboBox, _selectedTheme.TaskMode);
            AccentTextBox.Text = _selectedTheme.Accent;
            FocusXSlider.Value = _selectedTheme.FocusX;
            FocusYSlider.Value = _selectedTheme.FocusY;
            PreviewName.Text = _selectedTheme.Name;
            PreviewStatus.Text = _selectedTheme.IsActive ? "当前主题" : _selectedTheme.IsBundled ? "内置主题" : "用户主题";
            ReadOnlyHint.Text = _selectedTheme.IsBundled ? "内置主题为只读。导入一张图片即可创建可编辑的用户主题。" : "更改参数后选择“保存”；选择“设为当前”会让注入引擎使用此主题。";
            SaveButton.IsEnabled = !_selectedTheme.IsBundled;
            DeleteButton.IsEnabled = !_selectedTheme.IsBundled;
            ActivateButton.IsEnabled = !_selectedTheme.IsActive;
            SetEditorEnabled(!_selectedTheme.IsBundled);
            await LoadPreviewAsync(_selectedTheme.ImagePath);
        }
        finally { _isLoadingEditor = false; }
    }

    private static void SelectTag(ComboBox comboBox, string tag)
    {
        comboBox.SelectedItem = comboBox.Items.OfType<ComboBoxItem>().FirstOrDefault(item => string.Equals(item.Tag?.ToString(), tag, StringComparison.Ordinal));
    }

    private async Task LoadPreviewAsync(string path)
    {
        var file = await StorageFile.GetFileFromPathAsync(path);
        using var stream = await file.OpenReadAsync();
        var bitmap = new BitmapImage();
        await bitmap.SetSourceAsync(stream);
        PreviewImage.Source = bitmap;
    }

    private async void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.PicturesLibrary, ViewMode = PickerViewMode.Thumbnail };
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".webp");
            var window = ((App)Application.Current).MainWindow ?? throw new InvalidOperationException("主窗口尚未创建。");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(window));
            var file = await picker.PickSingleFileAsync();
            if (file is null) return;
            SetBusy(true);
            var imported = await _catalog.ImportAsync(file);
            await ReloadThemesAsync(imported.Id);
            ShowMessage("导入完成", "图片已经复制到应用数据目录并设为当前主题。", InfoBarSeverity.Success);
        }
        catch (Exception error) { ShowMessage("无法导入图片", error.Message, InfoBarSeverity.Error); }
        finally { SetBusy(false); }
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTheme is null || _selectedTheme.IsBundled) return;
        try
        {
            SetBusy(true);
            ReadEditorInto(_selectedTheme);
            await _catalog.SaveAsync(_selectedTheme);
            PreviewName.Text = _selectedTheme.Name;
            await ReloadThemesAsync(_selectedTheme.Id);
            ShowMessage("主题已保存", "新的构图与颜色参数已写入主题文件。", InfoBarSeverity.Success);
        }
        catch (Exception error) { ShowMessage("无法保存主题", error.Message, InfoBarSeverity.Error); }
        finally { SetBusy(false); }
    }

    private async void ActivateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTheme is null) return;
        try
        {
            SetBusy(true);
            if (!_selectedTheme.IsBundled)
            {
                ReadEditorInto(_selectedTheme);
                await _catalog.SaveAsync(_selectedTheme);
            }
            await _catalog.SelectAsync(_selectedTheme);
            var snapshot = await _engine.StartOrApplyAsync();
            await ReloadThemesAsync(_selectedTheme.Id);
            ShowMessage(snapshot.Summary, snapshot.Detail, snapshot.State == EngineState.Faulted ? InfoBarSeverity.Error : InfoBarSeverity.Informational);
        }
        catch (Exception error) { ShowMessage("无法切换主题", error.Message, InfoBarSeverity.Error); }
        finally { SetBusy(false); }
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTheme is null || _selectedTheme.IsBundled) return;
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "删除用户主题？",
            Content = $"将删除“{_selectedTheme.Name}”及其应用数据副本。原始图片不会受影响。",
            PrimaryButtonText = "删除",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        try
        {
            var id = _selectedTheme.Id;
            await _catalog.DeleteAsync(_selectedTheme);
            await ReloadThemesAsync();
            ShowMessage("主题已删除", $"用户主题 {id} 已从应用数据中移除。", InfoBarSeverity.Success);
        }
        catch (Exception error) { ShowMessage("无法删除主题", error.Message, InfoBarSeverity.Error); }
    }

    private void ReadEditorInto(ThemeDefinition theme)
    {
        theme.Name = NameTextBox.Text.Trim();
        theme.Appearance = (AppearanceComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "auto";
        theme.SafeArea = (SafeAreaComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "auto";
        theme.TaskMode = (TaskModeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "auto";
        theme.Accent = AccentTextBox.Text.Trim();
        theme.FocusX = FocusXSlider.Value;
        theme.FocusY = FocusYSlider.Value;
    }

    private void EditorValue_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingEditor || _selectedTheme is null) return;
        PreviewName.Text = string.IsNullOrWhiteSpace(NameTextBox.Text) ? "未命名主题" : NameTextBox.Text.Trim();
    }

    private void SetEditorEnabled(bool enabled)
    {
        NameTextBox.IsEnabled = enabled;
        AppearanceComboBox.IsEnabled = enabled;
        SafeAreaComboBox.IsEnabled = enabled;
        TaskModeComboBox.IsEnabled = enabled;
        AccentTextBox.IsEnabled = enabled;
        FocusXSlider.IsEnabled = enabled;
        FocusYSlider.IsEnabled = enabled;
    }

    private void SetBusy(bool busy)
    {
        ImportButton.IsEnabled = !busy;
        ThemesList.IsEnabled = !busy;
        if (busy) { SaveButton.IsEnabled = false; ActivateButton.IsEnabled = false; DeleteButton.IsEnabled = false; }
    }

    private void ShowMessage(string title, string message, InfoBarSeverity severity)
    {
        ThemeInfoBar.Title = title;
        ThemeInfoBar.Message = message;
        ThemeInfoBar.Severity = severity;
        ThemeInfoBar.IsOpen = true;
    }

    private void ThemesPage_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var narrow = e.NewSize.Width < 820;
        Grid.SetColumn(EditorPanel, narrow ? 0 : 1);
        Grid.SetRow(EditorPanel, narrow ? 1 : 0);
        LibraryColumn.Width = narrow ? new GridLength(1, GridUnitType.Star) : new GridLength(280);
        ThemesList.Height = narrow ? 260 : 430;
    }
}
