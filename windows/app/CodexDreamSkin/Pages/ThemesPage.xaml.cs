using CodexDreamSkin.Models;
using CodexDreamSkin.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;

namespace CodexDreamSkin.Pages;

public sealed partial class ThemesPage : Page
{
    private readonly ThemeCatalogService _catalog;
    private readonly CodexThemeEngine _engine;
    private readonly ThemePaletteService _paletteService = new();
    private readonly Stack<ThemeDraft> _undo = [];
    private readonly Stack<ThemeDraft> _redo = [];
    private readonly Dictionary<ImageSource, ThemeRegionSize> _sourceImageSizes = new(ReferenceEqualityComparer.Instance);
    private ThemeDefinition? _selectedTheme;
    private ThemeDraft? _savedDraft;
    private ThemeDraft? _currentDraft;
    private bool _isLoadingEditor;
    private bool _isBusy;
    private bool _isPreviewing;
    private bool _hasLoaded;
    private ThemeImageSlot _previewSlot = ThemeImageSlot.Background;
    private ThemeImageSlot _compositionSlot = ThemeImageSlot.Background;
    private ThemeComponentSlot _componentSlot = ThemeComponentSlot.Messages;
    private readonly Dictionary<ThemeImageSlot, ImagePaletteAnalysis> _imageAnalyses = [];
    private int _analysisGeneration;
    private CancellationTokenSource? _livePreviewDebounce;
    private int _livePreviewGeneration;
    private bool _isLiveSyncing;
    private uint? _cropPointerId;
    private Windows.Foundation.Point _cropLastPoint;
    private Windows.Foundation.Point _cropPressPoint;
    private ThemeDraft? _cropGestureStartDraft;
    private bool _cropGestureChanged;
    private ThemeRegionMetrics? _regionMetrics;
    private double _selectionLeft;
    private double _selectionTop;
    private double _selectionWidth;
    private double _selectionHeight;

    public ThemesPage()
    {
        InitializeComponent();
        var app = (App)Application.Current;
        _catalog = app.ThemeCatalog;
        _engine = app.ThemeEngine;
        NavigationCacheMode = NavigationCacheMode.Required;
        CompositionPreviewSurface.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(CompositionPreview_PointerPressed), true);
        CompositionPreviewSurface.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler(CompositionPreview_PointerMoved), true);
        CompositionPreviewSurface.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(CompositionPreview_PointerReleased), true);
        CompositionPreviewSurface.AddHandler(UIElement.PointerCanceledEvent, new PointerEventHandler(CompositionPreview_PointerCanceled), true);
        CompositionPreviewSurface.AddHandler(UIElement.PointerCaptureLostEvent, new PointerEventHandler(CompositionPreview_PointerCaptureLost), true);
        CompositionPreviewSurface.AddHandler(UIElement.PointerWheelChangedEvent, new PointerEventHandler(CompositionPreview_PointerWheelChanged), true);
    }

    private async void ThemesPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_hasLoaded)
        {
            UpdateEditorState();
            await RefreshActualRegionMetricsAsync();
            return;
        }
        _hasLoaded = true;
        await _catalog.CleanupAbandonedImagesAsync();
        await ReloadThemesAsync();
    }

    private async void ThemesPage_Unloaded(object sender, RoutedEventArgs e)
    {
        CancelLivePreviewWork();
        SetLivePreviewToggle(false);
        if (_isPreviewing) await RestoreActiveThemeAsync(false);
    }

    private async Task ReloadThemesAsync(string? selectId = null)
    {
        try
        {
            var themes = await _catalog.GetThemesAsync();
            var selected = themes.FirstOrDefault(theme => theme.Id == selectId)
                ?? themes.FirstOrDefault(theme => theme.IsActive)
                ?? themes.FirstOrDefault();
            _isLoadingEditor = true;
            ThemesList.ItemsSource = themes;
            ThemesList.SelectedItem = selected;
            _isLoadingEditor = false;
            if (selected is not null) await LoadThemeAsync(selected);
        }
        catch (Exception error) { ShowMessage("主题库加载失败", error.Message, InfoBarSeverity.Error); }
        finally { _isLoadingEditor = false; }
    }

    private async void ThemesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingEditor || ThemesList.SelectedItem is not ThemeDefinition candidate) return;
        var previous = _selectedTheme;
        if (previous is not null && previous.Id != candidate.Id && IsDirty)
        {
            if (!await ConfirmDiscardAsync())
            {
                _isLoadingEditor = true;
                ThemesList.SelectedItem = previous;
                _isLoadingEditor = false;
                return;
            }
            await _catalog.CleanupUnreferencedImagesAsync(previous);
        }

        if (_isPreviewing) await RestoreActiveThemeAsync(false);
        await LoadThemeAsync(candidate);
    }

    private async Task LoadThemeAsync(ThemeDefinition theme)
    {
        CancelLivePreviewWork();
        SetLivePreviewToggle(false);
        _selectedTheme = theme;
        _savedDraft = ThemeDraft.FromTheme(theme);
        _currentDraft = _savedDraft;
        _previewSlot = ThemeImageSlot.Background;
        _compositionSlot = ThemeImageSlot.Background;
        _componentSlot = ThemeComponentSlot.Messages;
        _undo.Clear();
        _redo.Clear();
        ApplyDraftToEditor(_savedDraft);
        PreviewStatus.Text = theme.IsActive ? "当前主题" : theme.IsBundled ? "内置主题" : "用户主题";
        ReadOnlyHint.Text = theme.IsBundled
            ? "内置主题为只读。选择“创建副本”后即可调整并保存。"
            : "编辑内容先保存在草稿中；可临时预览，保存后再正式应用。";
        LivePreviewStatusText.Text = theme.IsBundled
            ? (IsChinese ? "创建可编辑副本后可开启实时同步。" : "Create an editable copy to enable live sync.")
            : (IsChinese ? "开启后，草稿变化会在短暂防抖后自动预览，不会自动保存。" : "Draft changes preview after a short debounce and are never auto-saved.");
        SetEditorEnabled(!theme.IsBundled);
        _isLoadingEditor = true;
        SelectTag(CompositionSlotComboBox, ThemeImageSlot.Background.ToString());
        SelectTag(ComponentMaterialSlotComboBox, ThemeComponentSlot.Messages.ToString());
        _isLoadingEditor = false;
        await LoadImagePreviewsAsync(_savedDraft, theme.DirectoryPath);
        await RefreshActualRegionMetricsAsync();
        UpdateEditorState();
    }

    private static void SelectTag(ComboBox comboBox, string tag) =>
        comboBox.SelectedItem = comboBox.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), tag, StringComparison.Ordinal));

    private async Task LoadPreviewAsync(string path)
    {
        PreviewImage.Source = await LoadBitmapAsync(path);
        UpdateFocusMarker();
    }

    private async Task<BitmapImage> LoadBitmapAsync(string path)
    {
        var file = await StorageFile.GetFileFromPathAsync(path);
        using var stream = await file.OpenReadAsync();
        var decoder = await BitmapDecoder.CreateAsync(stream);
        var sourceSize = new ThemeRegionSize(decoder.PixelWidth, decoder.PixelHeight);
        stream.Seek(0);
        var bitmap = new BitmapImage();
        await bitmap.SetSourceAsync(stream);
        _sourceImageSizes[bitmap] = sourceSize;
        return bitmap;
    }

    private async Task LoadImagePreviewsAsync(ThemeDraft draft, string directory)
    {
        _sourceImageSizes.Clear();
        var background = await LoadBitmapAsync(Path.Combine(directory, draft.BackgroundImageFileName));
        var sidebar = draft.SidebarImageFileName == draft.BackgroundImageFileName
            ? background : await LoadBitmapAsync(Path.Combine(directory, draft.SidebarImageFileName));
        var composer = draft.ComposerImageFileName == draft.BackgroundImageFileName
            ? background : await LoadBitmapAsync(Path.Combine(directory, draft.ComposerImageFileName));
        var home = draft.HomeImageFileName == draft.BackgroundImageFileName
            ? background : await LoadBitmapAsync(Path.Combine(directory, draft.HomeImageFileName));
        var homeComposer = draft.HomeComposerImageFileName == draft.ComposerImageFileName
            ? composer : draft.HomeComposerImageFileName == draft.BackgroundImageFileName
                ? background : await LoadBitmapAsync(Path.Combine(directory, draft.HomeComposerImageFileName));
        var polaroid = draft.PolaroidImageFileName == draft.HomeImageFileName
            ? home : draft.PolaroidImageFileName == draft.BackgroundImageFileName
                ? background : await LoadBitmapAsync(Path.Combine(directory, draft.PolaroidImageFileName));
        BackgroundImagePreview.Source = background;
        SidebarImagePreview.Source = sidebar;
        ComposerImagePreview.Source = composer;
        HomeImagePreview.Source = home;
        HomeComposerImagePreview.Source = homeComposer;
        PolaroidImagePreview.Source = polaroid;
        SidebarModePreviewImage.Source = sidebar;
        MessageModePreviewImage.Source = background;
        ComposerModePreviewImage.Source = composer;
        HomeModePreviewImage.Source = home;
        HomeComposerModePreviewImage.Source = homeComposer;
        PolaroidModePreviewImage.Source = polaroid;
        await LoadPreviewAsync(Path.Combine(directory, GetImageFileName(draft, _previewSlot)));
        CompositionPreviewImage.Source = GetPreviewSource(_compositionSlot);
        UpdateCompositionPreview();
        UpdateAppearancePreview();
        ScheduleLivePreview();
        _ = RefreshImageAnalysesAsync(draft, directory);
    }

    private ImageSource? GetPreviewSource(ThemeImageSlot slot) => slot switch
    {
        ThemeImageSlot.Sidebar => SidebarImagePreview.Source,
        ThemeImageSlot.Composer => ComposerImagePreview.Source,
        ThemeImageSlot.Home => HomeImagePreview.Source,
        ThemeImageSlot.HomeComposer => HomeComposerImagePreview.Source,
        ThemeImageSlot.Polaroid => PolaroidImagePreview.Source,
        _ => BackgroundImagePreview.Source
    };

    private async void RefreshImagePreviews()
    {
        if (_currentDraft is null || _selectedTheme is null) return;
        try { await LoadImagePreviewsAsync(_currentDraft, _selectedTheme.DirectoryPath); }
        catch (Exception error) { ShowMessage("无法加载主题图片", error.Message, InfoBarSeverity.Error); }
    }

    private static string GetImageFileName(ThemeDraft draft, ThemeImageSlot slot) => slot switch
    {
        ThemeImageSlot.Sidebar => draft.SidebarImageFileName,
        ThemeImageSlot.Composer => draft.ComposerImageFileName,
        ThemeImageSlot.Home => draft.HomeImageFileName,
        ThemeImageSlot.HomeComposer => draft.HomeComposerImageFileName,
        ThemeImageSlot.Polaroid => draft.PolaroidImageFileName,
        _ => draft.BackgroundImageFileName
    };

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
            ShowMessage("导入完成", "图片已复制到主题库。编辑、保存后可正式应用。", InfoBarSeverity.Success);
        }
        catch (Exception error) { ShowMessage("无法导入图片", error.Message, InfoBarSeverity.Error); }
        finally { SetBusy(false); }
    }

    private async void ImportPackageButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.Downloads, ViewMode = PickerViewMode.List };
            picker.FileTypeFilter.Add(".cdxtheme");
            var window = ((App)Application.Current).MainWindow ?? throw new InvalidOperationException("主窗口尚未创建。");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(window));
            var file = await picker.PickSingleFileAsync();
            if (file is null) return;
            SetBusy(true);
            var imported = await _catalog.ImportPackageAsync(file);
            await ReloadThemesAsync(imported.Id);
            ShowMessage("主题包已导入", "主题已安全复制到应用主题库，并分配了新的本地标识。", InfoBarSeverity.Success);
        }
        catch (Exception error) { ShowMessage("无法导入主题包", error.Message, InfoBarSeverity.Error); }
        finally { SetBusy(false); }
    }

    private async void ImageSlotButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTheme?.IsBundled != false || _currentDraft is null || sender is not Button button
            || !Enum.TryParse<ThemeImageSlot>(button.Tag?.ToString(), out var slot)) return;
        try
        {
            var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.PicturesLibrary, ViewMode = PickerViewMode.Thumbnail };
            foreach (var extension in new[] { ".png", ".jpg", ".jpeg", ".webp" }) picker.FileTypeFilter.Add(extension);
            var window = ((App)Application.Current).MainWindow ?? throw new InvalidOperationException("主窗口尚未创建。");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(window));
            var file = await picker.PickSingleFileAsync();
            if (file is null) return;
            SetBusy(true);
            var stagedName = await _catalog.StageImageAsync(BuildDraftTheme(), slot, file);
            var previousBackground = _currentDraft.BackgroundImageFileName;
            var next = slot switch
            {
                ThemeImageSlot.Sidebar => _currentDraft with { SidebarImageFileName = stagedName },
                ThemeImageSlot.Composer => _currentDraft with { ComposerImageFileName = stagedName },
                ThemeImageSlot.Home => _currentDraft with
                {
                    HomeImageFileName = stagedName,
                    PolaroidImageFileName = _currentDraft.PolaroidImageFileName == _currentDraft.HomeImageFileName
                        ? stagedName : _currentDraft.PolaroidImageFileName
                },
                ThemeImageSlot.HomeComposer => _currentDraft with { HomeComposerImageFileName = stagedName },
                ThemeImageSlot.Polaroid => _currentDraft with { PolaroidImageFileName = stagedName },
                _ => _currentDraft with
                {
                    BackgroundImageFileName = stagedName,
                    SidebarImageFileName = _currentDraft.SidebarImageFileName == previousBackground ? stagedName : _currentDraft.SidebarImageFileName,
                    ComposerImageFileName = _currentDraft.ComposerImageFileName == previousBackground ? stagedName : _currentDraft.ComposerImageFileName,
                    HomeImageFileName = _currentDraft.HomeImageFileName == previousBackground ? stagedName : _currentDraft.HomeImageFileName,
                    HomeComposerImageFileName = _currentDraft.HomeComposerImageFileName == previousBackground ? stagedName : _currentDraft.HomeComposerImageFileName,
                    PolaroidImageFileName = _currentDraft.PolaroidImageFileName == previousBackground ? stagedName : _currentDraft.PolaroidImageFileName
                }
            };
            _undo.Push(_currentDraft);
            _currentDraft = next;
            _redo.Clear();
            _previewSlot = slot;
            _compositionSlot = slot;
            SelectTag(CompositionSlotComboBox, slot.ToString());
            ApplyDraftToEditor(next);
            RefreshImagePreviews();
            ShowMessage("区域图片已加入草稿", "保存主题后正式保留；撤销可恢复之前的图片。", InfoBarSeverity.Success);
        }
        catch (Exception error) { ShowMessage("无法更换区域图片", error.Message, InfoBarSeverity.Error); }
        finally { SetBusy(false); }
    }

    private void ResetImageSlot_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTheme?.IsBundled != false || _currentDraft is null || sender is not HyperlinkButton button
            || !Enum.TryParse<ThemeImageSlot>(button.Tag?.ToString(), out var slot) || slot == ThemeImageSlot.Background) return;
        var next = slot switch
        {
            ThemeImageSlot.Sidebar => _currentDraft with { SidebarImageFileName = _currentDraft.BackgroundImageFileName },
            ThemeImageSlot.Composer => _currentDraft with { ComposerImageFileName = _currentDraft.BackgroundImageFileName },
            ThemeImageSlot.Home => _currentDraft with
            {
                HomeImageFileName = _currentDraft.BackgroundImageFileName,
                PolaroidImageFileName = _currentDraft.PolaroidImageFileName == _currentDraft.HomeImageFileName
                    ? _currentDraft.BackgroundImageFileName : _currentDraft.PolaroidImageFileName
            },
            ThemeImageSlot.HomeComposer => _currentDraft with { HomeComposerImageFileName = _currentDraft.ComposerImageFileName },
            ThemeImageSlot.Polaroid => _currentDraft with { PolaroidImageFileName = _currentDraft.HomeImageFileName },
            _ => _currentDraft
        };
        if (next == _currentDraft) return;
        _undo.Push(_currentDraft);
        _currentDraft = next;
        _redo.Clear();
        _previewSlot = slot;
        _compositionSlot = slot;
        SelectTag(CompositionSlotComboBox, slot.ToString());
        ApplyDraftToEditor(next);
        RefreshImagePreviews();
        UpdateEditorState();
        ShowMessage("区域图片已跟随主背景", "保存主题后正式保留；后续更换主背景时，此区域会同步更新。", InfoBarSeverity.Success);
    }

    private async void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTheme is null || IsDirty) return;
        try
        {
            var picker = new FileSavePicker { SuggestedStartLocation = PickerLocationId.Downloads };
            picker.FileTypeChoices.Add("Codex theme package", [".cdxtheme"]);
            picker.SuggestedFileName = SanitizeSuggestedFileName(_selectedTheme.Name);
            var window = ((App)Application.Current).MainWindow ?? throw new InvalidOperationException("主窗口尚未创建。");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(window));
            var file = await picker.PickSaveFileAsync();
            if (file is null) return;
            SetBusy(true);
            await _catalog.ExportPackageAsync(_selectedTheme, file);
            ShowMessage("主题包已导出", $"已保存到 {file.Path}", InfoBarSeverity.Success);
        }
        catch (Exception error) { ShowMessage("无法导出主题包", error.Message, InfoBarSeverity.Error); }
        finally { SetBusy(false); }
    }

    private async void HistoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTheme is null || _selectedTheme.IsBundled || IsDirty || _isPreviewing) return;
        try
        {
            SetBusy(true);
            var history = await _catalog.GetHistoryAsync(_selectedTheme);
            if (history.Count == 0)
            {
                ShowMessage("暂无历史版本", "首次修改并保存后，这里会保留保存前的版本。", InfoBarSeverity.Informational);
                return;
            }

            var list = new ListView
            {
                ItemsSource = history,
                SelectionMode = ListViewSelectionMode.Single,
                SelectedIndex = 0,
                MaxHeight = 360,
                MinWidth = 360
            };
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "恢复主题历史",
                Content = list,
                PrimaryButtonText = "恢复此版本",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Close
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary || list.SelectedItem is not ThemeHistoryEntry selected) return;

            var restored = await _catalog.RestoreHistoryAsync(_selectedTheme, selected);
            await ReloadThemesAsync(restored.Id);
            ShowMessage("历史版本已恢复", "恢复前的当前版本也已自动存入历史。", InfoBarSeverity.Success);
        }
        catch (Exception error) { ShowMessage("无法恢复历史版本", error.Message, InfoBarSeverity.Error); }
        finally { SetBusy(false); }
    }

    private static string SanitizeSuggestedFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Trim().Select(character => invalid.Contains(character) ? '-' : character).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "codex-theme" : sanitized[..Math.Min(sanitized.Length, 80)];
    }

    private async void DuplicateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTheme is null) return;
        try
        {
            if (_isPreviewing) await RestoreActiveThemeAsync(false);
            SetBusy(true);
            var duplicate = await _catalog.DuplicateAsync(BuildDraftTheme());
            await ReloadThemesAsync(duplicate.Id);
            ShowMessage("副本已创建", "现在可以安全编辑副本，内置主题不会被修改。", InfoBarSeverity.Success);
        }
        catch (Exception error) { ShowMessage("无法创建副本", error.Message, InfoBarSeverity.Error); }
        finally { SetBusy(false); }
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTheme is null || _selectedTheme.IsBundled || _currentDraft is null) return;
        try
        {
            if (_isPreviewing) await RestoreActiveThemeAsync(false);
            SetBusy(true);
            ApplyDraftToTheme(_currentDraft, _selectedTheme);
            await _catalog.SaveAsync(_selectedTheme);
            await ReloadThemesAsync(_selectedTheme.Id);
            ShowMessage("主题已保存", "草稿参数已原子写入主题文件。", InfoBarSeverity.Success);
        }
        catch (Exception error) { ShowMessage("无法保存主题", error.Message, InfoBarSeverity.Error); }
        finally { SetBusy(false); }
    }

    private async void PreviewButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTheme is null || _currentDraft is null) return;
        try
        {
            SetBusy(true);
            var previewDirectory = await _catalog.PreparePreviewAsync(BuildDraftTheme());
            var snapshot = await _engine.PreviewAsync(previewDirectory);
            _isPreviewing = snapshot.State == EngineState.Active;
            ShowMessage(snapshot.Summary, snapshot.Detail, snapshot.State == EngineState.Faulted ? InfoBarSeverity.Error : InfoBarSeverity.Informational);
        }
        catch (Exception error) { ShowMessage("无法预览草稿", error.Message, InfoBarSeverity.Error); }
        finally { SetBusy(false); }
    }

    private async void CancelPreviewButton_Click(object sender, RoutedEventArgs e) => await RestoreActiveThemeAsync(true);

    private async Task RestoreActiveThemeAsync(bool showStatus)
    {
        CancelLivePreviewWork();
        SetLivePreviewToggle(false);
        if (!_isPreviewing) return;
        try
        {
            SetBusy(true);
            var snapshot = await _engine.StartOrApplyAsync();
            _isPreviewing = false;
            await _catalog.CleanupPreviewAsync();
            if (showStatus) ShowMessage("预览已取消", snapshot.Detail, snapshot.State == EngineState.Faulted ? InfoBarSeverity.Error : InfoBarSeverity.Success);
        }
        catch (Exception error)
        {
            if (showStatus) ShowMessage("无法恢复当前主题", error.Message, InfoBarSeverity.Error);
        }
        finally { SetBusy(false); }
    }

    private async void LivePreviewToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isLoadingEditor) return;
        if (LivePreviewToggle.IsOn)
        {
            LivePreviewStatusText.Text = IsChinese ? "等待同步最新草稿…" : "Waiting to sync the latest draft…";
            ScheduleLivePreview(immediate: true);
            UpdateEditorState();
            return;
        }

        CancelLivePreviewWork();
        LivePreviewStatusText.Text = IsChinese ? "实时同步已关闭" : "Live sync is off";
        if (_isPreviewing) await RestoreActiveThemeAsync(false);
        UpdateEditorState();
    }

    private void ScheduleLivePreview(bool immediate = false)
    {
        if (!LivePreviewToggle.IsOn || _selectedTheme?.IsBundled != false || _currentDraft is null) return;
        var draft = BuildDraftTheme();
        var generation = Interlocked.Increment(ref _livePreviewGeneration);
        var cancellation = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _livePreviewDebounce, cancellation);
        previous?.Cancel();
        _ = ApplyLivePreviewAsync(draft, generation, immediate, cancellation);
    }

    private async Task ApplyLivePreviewAsync(ThemeDefinition draft, int generation, bool immediate, CancellationTokenSource cancellation)
    {
        var cancellationToken = cancellation.Token;
        try
        {
            if (!immediate) await Task.Delay(240, cancellationToken);
            if (generation != _livePreviewGeneration) return;
            _isLiveSyncing = true;
            LivePreviewProgressRing.IsActive = true;
            LivePreviewStatusText.Text = IsChinese ? "正在热重载到 Codex…" : "Hot reloading into Codex…";
            UpdateEditorState();

            var previewDirectory = await _catalog.PreparePreviewAsync(draft, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = _isPreviewing
                ? await _engine.RefreshPreviewAsync(previewDirectory, cancellationToken)
                : await _engine.PreviewAsync(previewDirectory, cancellationToken);
            if (generation != _livePreviewGeneration) return;
            _isPreviewing = snapshot.State == EngineState.Active;
            LivePreviewStatusText.Text = snapshot.State == EngineState.Active
                ? (IsChinese ? $"已同步 · {snapshot.TargetCount} 个页面" : $"Synced · {snapshot.TargetCount} page(s)")
                : snapshot.Summary;
            if (snapshot.State == EngineState.Faulted)
                ShowMessage(snapshot.Summary, snapshot.Detail, InfoBarSeverity.Error);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception error)
        {
            if (generation == _livePreviewGeneration)
            {
                LivePreviewStatusText.Text = IsChinese ? "实时同步失败" : "Live sync failed";
                ShowMessage(IsChinese ? "无法实时同步草稿" : "Unable to live-sync the draft", error.Message, InfoBarSeverity.Error);
            }
        }
        finally
        {
            Interlocked.CompareExchange(ref _livePreviewDebounce, null, cancellation);
            cancellation.Dispose();
            if (generation == _livePreviewGeneration)
            {
                _isLiveSyncing = false;
                LivePreviewProgressRing.IsActive = false;
                UpdateEditorState();
            }
        }
    }

    private void CancelLivePreviewWork()
    {
        Interlocked.Increment(ref _livePreviewGeneration);
        var cancellation = Interlocked.Exchange(ref _livePreviewDebounce, null);
        cancellation?.Cancel();
        _isLiveSyncing = false;
        if (LivePreviewProgressRing is not null) LivePreviewProgressRing.IsActive = false;
    }

    private void SetLivePreviewToggle(bool isOn)
    {
        if (LivePreviewToggle is null || LivePreviewToggle.IsOn == isOn) return;
        var wasLoading = _isLoadingEditor;
        _isLoadingEditor = true;
        LivePreviewToggle.IsOn = isOn;
        _isLoadingEditor = wasLoading;
    }

    private static bool IsChinese => System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh";

    private async void ActivateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTheme is null || IsDirty) return;
        try
        {
            SetBusy(true);
            await _catalog.SelectAsync(_selectedTheme);
            var snapshot = await _engine.StartOrApplyAsync();
            _isPreviewing = false;
            await _catalog.CleanupPreviewAsync();
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
            SetBusy(true);
            var id = _selectedTheme.Id;
            await _catalog.DeleteAsync(_selectedTheme);
            await ReloadThemesAsync();
            ShowMessage("主题已删除", $"用户主题 {id} 已从应用数据中移除。", InfoBarSeverity.Success);
        }
        catch (Exception error) { ShowMessage("无法删除主题", error.Message, InfoBarSeverity.Error); }
        finally { SetBusy(false); }
    }

    private void UndoButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentDraft is null || _undo.Count == 0) return;
        _redo.Push(_currentDraft);
        _currentDraft = _undo.Pop();
        ApplyDraftToEditor(_currentDraft);
        RefreshImagePreviews();
        UpdateEditorState();
    }

    private void RedoButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentDraft is null || _redo.Count == 0) return;
        _undo.Push(_currentDraft);
        _currentDraft = _redo.Pop();
        ApplyDraftToEditor(_currentDraft);
        RefreshImagePreviews();
        UpdateEditorState();
    }

    private void EditorValue_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingEditor || _selectedTheme is null || _selectedTheme.IsBundled) return;
        var next = ReadEditorDraft();
        if (_currentDraft == next) return;
        if (_currentDraft is not null) _undo.Push(_currentDraft);
        _currentDraft = next;
        _redo.Clear();
        PreviewName.Text = string.IsNullOrWhiteSpace(next.Name) ? "未命名主题" : next.Name;
        if (ReferenceEquals(sender, AccentTextBox)) SyncColorPicker(next.Accent);
        UpdateFocusMarker();
        UpdateCompositionPreview();
        UpdateAppearancePreview();
        UpdateComponentMaterialPreview();
        UpdateEditorState();
        ScheduleLivePreview();
    }

    private void AccentColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (_isLoadingEditor || _selectedTheme?.IsBundled != false) return;
        AccentTextBox.Text = $"#{args.NewColor.R:X2}{args.NewColor.G:X2}{args.NewColor.B:X2}";
    }

    private void PreviewSurface_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_selectedTheme?.IsBundled != false || PreviewSurface.ActualWidth <= 0 || PreviewSurface.ActualHeight <= 0) return;
        var position = e.GetCurrentPoint(PreviewSurface).Position;
        CompositionFocusXSlider.Value = Math.Clamp(position.X / PreviewSurface.ActualWidth, 0, 1);
        CompositionFocusYSlider.Value = Math.Clamp(position.Y / PreviewSurface.ActualHeight, 0, 1);
        e.Handled = true;
    }

    private void PreviewSurface_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateFocusMarker();

    private ThemeDraft ReadEditorDraft()
    {
        var seed = _currentDraft ?? (_selectedTheme is not null ? ThemeDraft.FromTheme(_selectedTheme) : throw new InvalidOperationException("尚未选择主题。"));
        var next = seed with
        {
            Name = NameTextBox.Text.Trim(),
            Appearance = GetTag(AppearanceComboBox, "auto"),
            SafeArea = GetTag(SafeAreaComboBox, "auto"),
            TaskMode = GetTag(TaskModeComboBox, "auto"),
            Accent = AccentTextBox.Text.Trim(),
            LightPageOpacity = LightPageOpacitySlider.Value,
            LightSidebarOpacity = LightSidebarOpacitySlider.Value,
            LightComposerOpacity = LightComposerOpacitySlider.Value,
            LightCardOpacity = LightCardOpacitySlider.Value,
            DarkPageOpacity = DarkPageOpacitySlider.Value,
            DarkSidebarOpacity = DarkSidebarOpacitySlider.Value,
            DarkComposerOpacity = DarkComposerOpacitySlider.Value,
            DarkCardOpacity = DarkCardOpacitySlider.Value,
            ComponentMaterials = seed.ComponentMaterials.Set(
                _componentSlot,
                ReadComponentMaterialControls(seed.ComponentMaterials.Get(_componentSlot)))
        };
        return SetComposition(next, _compositionSlot, ReadCompositionControls());
    }

    private void ApplyDraftToEditor(ThemeDraft draft)
    {
        _isLoadingEditor = true;
        NameTextBox.Text = draft.Name;
        SelectTag(AppearanceComboBox, draft.Appearance);
        SelectTag(SafeAreaComboBox, draft.SafeArea);
        SelectTag(TaskModeComboBox, draft.TaskMode);
        AccentTextBox.Text = draft.Accent;
        ApplyCompositionControls(GetComposition(draft, _compositionSlot));
        LightPageOpacitySlider.Value = draft.LightPageOpacity;
        LightSidebarOpacitySlider.Value = draft.LightSidebarOpacity;
        LightComposerOpacitySlider.Value = draft.LightComposerOpacity;
        LightCardOpacitySlider.Value = draft.LightCardOpacity;
        DarkPageOpacitySlider.Value = draft.DarkPageOpacity;
        DarkSidebarOpacitySlider.Value = draft.DarkSidebarOpacity;
        DarkComposerOpacitySlider.Value = draft.DarkComposerOpacity;
        DarkCardOpacitySlider.Value = draft.DarkCardOpacity;
        ApplyComponentMaterialControls(draft.ComponentMaterials.Get(_componentSlot));
        PreviewName.Text = draft.Name;
        SyncColorPicker(draft.Accent);
        _isLoadingEditor = false;
        UpdateImageSlotStates(draft);
        UpdateFocusMarker();
        UpdateCompositionPreview();
        UpdateAppearancePreview();
        UpdateComponentMaterialPreview();
        ScheduleLivePreview();
    }

    private void ComponentMaterialSlotComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ComponentMaterialSlotComboBox.SelectedItem is not ComboBoxItem item
            || !Enum.TryParse<ThemeComponentSlot>(item.Tag?.ToString(), out var slot)) return;
        _componentSlot = slot;
        if (_currentDraft is null) return;
        var wasLoading = _isLoadingEditor;
        _isLoadingEditor = true;
        ApplyComponentMaterialControls(_currentDraft.ComponentMaterials.Get(slot));
        _isLoadingEditor = wasLoading;
        UpdateComponentMaterialPreview();
        UpdateEditorState();
    }

    private ThemeComponentMaterial ReadComponentMaterialControls(ThemeComponentMaterial fallback) => new(
        NormalizeComponentColor(LightComponentColorTextBox.Text, fallback.LightColor),
        LightComponentOpacitySlider.Value,
        NormalizeComponentColor(DarkComponentColorTextBox.Text, fallback.DarkColor),
        DarkComponentOpacitySlider.Value);

    private void ApplyComponentMaterialControls(ThemeComponentMaterial value)
    {
        LightComponentColorTextBox.Text = value.LightColor;
        LightComponentOpacitySlider.Value = value.LightOpacity;
        DarkComponentColorTextBox.Text = value.DarkColor;
        DarkComponentOpacitySlider.Value = value.DarkOpacity;
    }

    private static string NormalizeComponentColor(string value, string fallback)
    {
        if (!TryParseColor(value, out var color) || value.Trim().TrimStart('#').Length != 6) return fallback;
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private void UpdateComponentMaterialPreview()
    {
        if (_currentDraft is null) return;
        var value = _currentDraft.ComponentMaterials.Get(_componentSlot);
        LightComponentPreview.Background = ComponentBrush(value.LightColor, value.LightOpacity);
        DarkComponentPreview.Background = ComponentBrush(value.DarkColor, value.DarkOpacity);
    }

    private static SolidColorBrush ComponentBrush(string colorText, double opacity)
    {
        if (!TryParseColor(colorText, out var color)) color = Color.FromArgb(255, 21, 87, 176);
        return new SolidColorBrush(Color.FromArgb((byte)Math.Round(Math.Clamp(opacity, 0, 1) * 255), color.R, color.G, color.B));
    }

    private void ResetComponentMaterialButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTheme?.IsBundled != false || _currentDraft is null) return;
        var recommended = ThemeComponentMaterials.Default.Get(_componentSlot);
        var next = _currentDraft with { ComponentMaterials = _currentDraft.ComponentMaterials.Set(_componentSlot, recommended) };
        if (next == _currentDraft) return;
        _undo.Push(_currentDraft);
        _currentDraft = next;
        _redo.Clear();
        ApplyDraftToEditor(next);
        UpdateEditorState();
    }

    private void UpdateImageSlotStates(ThemeDraft draft)
    {
        var inheritedText = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh"
            ? "跟随主背景"
            : "Follows main background";
        var independentText = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh"
            ? "独立图片"
            : "Independent image";
        SidebarImageState.Text = draft.SidebarImageFileName == draft.BackgroundImageFileName ? inheritedText : independentText;
        ComposerImageState.Text = draft.ComposerImageFileName == draft.BackgroundImageFileName ? inheritedText : independentText;
        HomeImageState.Text = draft.HomeImageFileName == draft.BackgroundImageFileName ? inheritedText : independentText;
        HomeComposerImageState.Text = draft.HomeComposerImageFileName == draft.ComposerImageFileName
            ? (IsChinese ? "跟随任务输入框" : "Follows task composer") : independentText;
        PolaroidImageState.Text = draft.PolaroidImageFileName == draft.HomeImageFileName
            ? (IsChinese ? "跟随首页照片框" : "Follows home frame") : independentText;
    }

    private static ThemeComposition GetComposition(ThemeDraft draft, ThemeImageSlot slot) => slot switch
    {
        ThemeImageSlot.Sidebar => draft.SidebarComposition,
        ThemeImageSlot.Composer => draft.ComposerComposition,
        ThemeImageSlot.Home => draft.HomeComposition,
        ThemeImageSlot.HomeComposer => draft.HomeComposerComposition,
        ThemeImageSlot.Polaroid => draft.PolaroidComposition,
        _ => draft.BackgroundComposition
    };

    private static ThemeDraft SetComposition(ThemeDraft draft, ThemeImageSlot slot, ThemeComposition composition) => slot switch
    {
        ThemeImageSlot.Sidebar => draft with { SidebarComposition = composition },
        ThemeImageSlot.Composer => draft with { ComposerComposition = composition },
        ThemeImageSlot.Home => draft with { HomeComposition = composition },
        ThemeImageSlot.HomeComposer => draft with { HomeComposerComposition = composition },
        ThemeImageSlot.Polaroid => draft with { PolaroidComposition = composition },
        _ => draft with { BackgroundComposition = composition }
    };

    private ThemeComposition ReadCompositionControls() => new(
        CompositionFocusXSlider.Value,
        CompositionFocusYSlider.Value,
        CompositionZoomSlider.Value,
        GetTag(CompositionFitComboBox, "auto"),
        CompositionOffsetXSlider.Value,
        CompositionOffsetYSlider.Value);

    private void ApplyCompositionControls(ThemeComposition composition)
    {
        CompositionFocusXSlider.Value = composition.FocusX;
        CompositionFocusYSlider.Value = composition.FocusY;
        CompositionZoomSlider.Value = composition.Zoom;
        SelectTag(CompositionFitComboBox, composition.Fit);
        CompositionOffsetXSlider.Value = composition.OffsetX;
        CompositionOffsetYSlider.Value = composition.OffsetY;
    }

    private async void CompositionSlot_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingEditor || _currentDraft is null || _selectedTheme is null) return;
        if (!Enum.TryParse<ThemeImageSlot>(GetTag(CompositionSlotComboBox, "Background"), out var slot)) slot = ThemeImageSlot.Background;
        _compositionSlot = slot;
        _previewSlot = slot;
        CompositionSelectionCanvas.Visibility = Visibility.Collapsed;
        var wasLoading = _isLoadingEditor;
        _isLoadingEditor = true;
        ApplyCompositionControls(GetComposition(_currentDraft, slot));
        _isLoadingEditor = wasLoading;
        CompositionPreviewImage.Source = GetPreviewSource(slot);
        await LoadPreviewAsync(Path.Combine(_selectedTheme.DirectoryPath, GetImageFileName(_currentDraft, slot)));
        UpdateCompositionPreview();
        UpdateEditorState();
    }

    private void CompositionPreview_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_selectedTheme?.IsBundled != false || _currentDraft is null
            || CompositionPreviewSurface.ActualWidth <= 0 || CompositionPreviewSurface.ActualHeight <= 0) return;
        var point = e.GetCurrentPoint(CompositionPreviewSurface);
        if (point.Properties.IsRightButtonPressed || point.Properties.IsMiddleButtonPressed) return;
        _cropPointerId = e.Pointer.PointerId;
        _cropLastPoint = point.Position;
        _cropPressPoint = point.Position;
        _cropGestureStartDraft = _currentDraft;
        _cropGestureChanged = false;
        CompositionPreviewSurface.CapturePointer(e.Pointer);
        CompositionPreviewSurface.Focus(FocusState.Pointer);
        e.Handled = true;
    }

    private void CompositionPreview_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_cropPointerId != e.Pointer.PointerId || _currentDraft is null) return;
        var point = e.GetCurrentPoint(CompositionPreviewSurface);
        var deltaX = point.Position.X - _cropLastPoint.X;
        var deltaY = point.Position.Y - _cropLastPoint.Y;
        _cropLastPoint = point.Position;
        if (Math.Abs(deltaX) < 0.25 && Math.Abs(deltaY) < 0.25) return;
        var current = GetComposition(_currentDraft, _compositionSlot);
        var horizontalRange = Math.Max(0, CompositionPreviewSurface.ActualWidth - _selectionWidth);
        var verticalRange = Math.Max(0, CompositionPreviewSurface.ActualHeight - _selectionHeight);
        var nextLeft = Math.Clamp(_selectionLeft + deltaX, 0, horizontalRange);
        var nextTop = Math.Clamp(_selectionTop + deltaY, 0, verticalRange);
        var next = current with
        {
            Fit = "cover",
            FocusX = horizontalRange > 0.5 ? nextLeft / horizontalRange : 0.5,
            FocusY = verticalRange > 0.5 ? nextTop / verticalRange : 0.5,
            OffsetX = 0,
            OffsetY = 0
        };
        if (next == current) return;
        _currentDraft = SetComposition(_currentDraft, _compositionSlot, next);
        _cropGestureChanged = true;
        ApplyCropDraftToControls(next);
        e.Handled = true;
    }

    private void CompositionPreview_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_cropPointerId != e.Pointer.PointerId) return;
        CompleteCropGesture(e.GetCurrentPoint(CompositionPreviewSurface).Position, true);
        e.Handled = true;
    }

    private void CompositionPreview_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (_cropPointerId != e.Pointer.PointerId) return;
        CompleteCropGesture(null, false);
    }

    private void CompositionPreview_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (_cropPointerId == e.Pointer.PointerId) CompleteCropGesture(null, false);
    }

    private void CompleteCropGesture(Windows.Foundation.Point? releasePoint, bool allowClickFocus)
    {
        var pointerId = _cropPointerId;
        _cropPointerId = null;
        if (pointerId is not null)
        {
            foreach (var pointer in CompositionPreviewSurface.PointerCaptures.ToArray())
                if (pointer.PointerId == pointerId.Value) CompositionPreviewSurface.ReleasePointerCapture(pointer);
        }
        if (_currentDraft is null) return;
        if (_cropGestureChanged && _cropGestureStartDraft is not null && _cropGestureStartDraft != _currentDraft)
        {
            _undo.Push(_cropGestureStartDraft);
            _redo.Clear();
            UpdateEditorState();
            ScheduleLivePreview();
        }
        else if (allowClickFocus && releasePoint is { } point
                 && Math.Abs(point.X - _cropPressPoint.X) < 4 && Math.Abs(point.Y - _cropPressPoint.Y) < 4)
        {
            var current = GetComposition(_currentDraft, _compositionSlot);
            var horizontalRange = Math.Max(0, CompositionPreviewSurface.ActualWidth - _selectionWidth);
            var verticalRange = Math.Max(0, CompositionPreviewSurface.ActualHeight - _selectionHeight);
            var nextLeft = Math.Clamp(point.X - _selectionWidth / 2, 0, horizontalRange);
            var nextTop = Math.Clamp(point.Y - _selectionHeight / 2, 0, verticalRange);
            var next = current with
            {
                FocusX = horizontalRange > 0.5 ? nextLeft / horizontalRange : 0.5,
                FocusY = verticalRange > 0.5 ? nextTop / verticalRange : 0.5,
                Fit = "cover",
                OffsetX = 0,
                OffsetY = 0
            };
            ApplyCompositionDraft(SetComposition(_currentDraft, _compositionSlot, next), IsChinese ? "已重新定位取景框" : "Viewport repositioned");
        }
        _cropGestureStartDraft = null;
        _cropGestureChanged = false;
    }

    private void CompositionPreview_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (_selectedTheme?.IsBundled != false || _currentDraft is null) return;
        var controlState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control);
        if ((controlState & Windows.UI.Core.CoreVirtualKeyStates.Down) == 0) return;
        var delta = e.GetCurrentPoint(CompositionPreviewSurface).Properties.MouseWheelDelta;
        if (delta == 0) return;
        ChangeCropZoom(delta > 0 ? 0.1 : -0.1);
        e.Handled = true;
    }

    private void CompositionPreview_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_selectedTheme?.IsBundled != false || _currentDraft is null) return;
        var current = GetComposition(_currentDraft, _compositionSlot);
        var next = e.Key switch
        {
            Windows.System.VirtualKey.Left => current with { Fit = "cover", FocusX = Math.Clamp(current.FocusX - 0.04, 0, 1), OffsetX = 0 },
            Windows.System.VirtualKey.Right => current with { Fit = "cover", FocusX = Math.Clamp(current.FocusX + 0.04, 0, 1), OffsetX = 0 },
            Windows.System.VirtualKey.Up => current with { Fit = "cover", FocusY = Math.Clamp(current.FocusY - 0.04, 0, 1), OffsetY = 0 },
            Windows.System.VirtualKey.Down => current with { Fit = "cover", FocusY = Math.Clamp(current.FocusY + 0.04, 0, 1), OffsetY = 0 },
            Windows.System.VirtualKey.Add or Windows.System.VirtualKey.GamepadRightShoulder => current with { Fit = "cover", Zoom = Math.Clamp(current.Zoom + 0.1, 0.5, 3) },
            Windows.System.VirtualKey.Subtract or Windows.System.VirtualKey.GamepadLeftShoulder => current with { Fit = "cover", Zoom = Math.Clamp(current.Zoom - 0.1, 0.5, 3) },
            _ => current
        };
        if (next == current) return;
        ApplyCompositionDraft(SetComposition(_currentDraft, _compositionSlot, next), IsChinese ? "已微调取景框" : "Viewport adjusted");
        e.Handled = true;
    }

    private void ChangeCropZoom(double delta)
    {
        if (_currentDraft is null) return;
        var current = GetComposition(_currentDraft, _compositionSlot);
        var next = current with { Fit = "cover", Zoom = Math.Clamp(Math.Round((current.Zoom + delta) * 20) / 20, 0.5, 3) };
        if (next == current) return;
        ApplyCompositionDraft(SetComposition(_currentDraft, _compositionSlot, next), IsChinese ? "已缩放取景框" : "Viewport size changed");
    }

    private void ApplyCropDraftToControls(ThemeComposition composition)
    {
        var wasLoading = _isLoadingEditor;
        _isLoadingEditor = true;
        ApplyCompositionControls(composition);
        _isLoadingEditor = wasLoading;
        UpdateCompositionPreview();
        UpdateAppearancePreview();
        UpdateEditorState();
        ScheduleLivePreview();
    }

    private void CompositionPreview_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateCompositionPreview();

    private void CompositionPreviewImage_ImageOpened(object sender, RoutedEventArgs e) => UpdateCompositionPreview();

    private void CompositionPreviewImage_ImageFailed(object sender, ExceptionRoutedEventArgs e)
    {
        CompositionSelectionCanvas.Visibility = Visibility.Collapsed;
    }

    private async void RefreshActualRegionButton_Click(object sender, RoutedEventArgs e) => await RefreshActualRegionMetricsAsync();

    private async Task RefreshActualRegionMetricsAsync()
    {
        RefreshActualRegionButton.IsEnabled = false;
        ActualRegionStatusText.Text = IsChinese ? "正在读取 Codex 实际区域…" : "Reading live Codex regions…";
        try
        {
            _regionMetrics = await _engine.MeasureRegionsAsync();
            ActualRegionStatusText.Text = _regionMetrics is null
                ? (IsChinese ? "未连接到主题端口，预览暂用自适应参考比例。" : "Theme port unavailable; adaptive reference ratios are in use.")
                : (IsChinese
                    ? $"已同步 Codex {_regionMetrics.Viewport.Width:F0}×{_regionMetrics.Viewport.Height:F0} 实际布局。"
                    : $"Synced to live Codex {_regionMetrics.Viewport.Width:F0}×{_regionMetrics.Viewport.Height:F0} layout.");
            UpdateCompositionPreview();
            UpdateAppearancePreview();
        }
        catch (Exception error)
        {
            _regionMetrics = null;
            ActualRegionStatusText.Text = IsChinese ? $"读取失败：{error.Message}" : $"Read failed: {error.Message}";
            UpdateCompositionPreview();
            UpdateAppearancePreview();
        }
        finally { RefreshActualRegionButton.IsEnabled = true; }
    }

    private void UpdateCompositionPreview()
    {
        if (_currentDraft is null) return;
        var composition = GetComposition(_currentDraft, _compositionSlot);
        var available = Math.Max(180, EditorCanvasHost.ActualWidth - 4);
        var fallback = _compositionSlot switch
        {
            ThemeImageSlot.Sidebar => new ThemeRegionSize(275, 998),
            ThemeImageSlot.Composer => new ThemeRegionSize(736, 98),
            ThemeImageSlot.Home => new ThemeRegionSize(1603, 215),
            ThemeImageSlot.HomeComposer => new ThemeRegionSize(920, 98),
            ThemeImageSlot.Polaroid => new ThemeRegionSize(122, 158),
            _ => new ThemeRegionSize(1647, 998)
        };
        var measured = _regionMetrics?.Get(_compositionSlot);
        var region = measured is { IsValid: true } ? measured : fallback;
        var ratio = region.Ratio;
        var slotName = IsChinese
            ? _compositionSlot switch { ThemeImageSlot.Sidebar => "左侧栏", ThemeImageSlot.Composer => "任务输入框", ThemeImageSlot.Home => "首页照片框", ThemeImageSlot.HomeComposer => "首页输入框", ThemeImageSlot.Polaroid => "首页宝丽来照片框", _ => "主背景" }
            : _compositionSlot switch { ThemeImageSlot.Sidebar => "Sidebar", ThemeImageSlot.Composer => "Task composer", ThemeImageSlot.Home => "Home frame", ThemeImageSlot.HomeComposer => "Home composer", ThemeImageSlot.Polaroid => "Home Polaroid frame", _ => "Background" };
        var source = measured is null ? (IsChinese ? "参考" : "reference") : (IsChinese ? "实际" : "live");
        CropTargetText.Text = $"{slotName} · {region.Width:F0}×{region.Height:F0} · {ratio:F3}:1 · {source}";
        var sourceBitmap = GetPreviewSource(_compositionSlot) as BitmapImage;
        var sourceSize = sourceBitmap is not null && _sourceImageSizes.TryGetValue(sourceBitmap, out var decodedSize)
            ? decodedSize
            : sourceBitmap is { PixelWidth: > 0, PixelHeight: > 0 }
                ? new ThemeRegionSize(sourceBitmap.PixelWidth, sourceBitmap.PixelHeight)
                : null;
        var sourceRatio = sourceSize is { IsValid: true }
            ? sourceSize.Ratio
            : 16d / 9d;
        const double maximumHeight = 520;
        var frameWidth = available;
        var frameHeight = Math.Min(frameWidth / sourceRatio, maximumHeight);
        CompositionPreviewFrame.Width = frameWidth;
        CompositionPreviewFrame.Height = frameHeight;
        var previewSource = GetPreviewSource(_compositionSlot);
        if (!ReferenceEquals(CompositionPreviewImage.Source, previewSource)) CompositionPreviewImage.Source = previewSource;
        CompositionPreviewImage.Width = double.NaN;
        CompositionPreviewImage.Height = double.NaN;
        CompositionPreviewImage.HorizontalAlignment = HorizontalAlignment.Stretch;
        CompositionPreviewImage.VerticalAlignment = VerticalAlignment.Stretch;
        CompositionPreviewImage.Stretch = Stretch.Uniform;
        CompositionPreviewImage.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
        CompositionPreviewImage.RenderTransform = new CompositeTransform();
        CompositionSelectionCanvas.Width = frameWidth;
        CompositionSelectionCanvas.Height = frameHeight;
        if (UpdateSelectionFrame(composition, ratio, frameWidth, frameHeight, sourceSize))
            CropTargetText.Text += IsChinese
                ? $" · 取景 {_selectionWidth:F0}×{_selectionHeight:F0}"
                : $" · viewport {_selectionWidth:F0}×{_selectionHeight:F0}";
    }

    private bool UpdateSelectionFrame(
        ThemeComposition composition,
        double targetRatio,
        double displayWidth,
        double displayHeight,
        ThemeRegionSize? sourceSize)
    {
        if (sourceSize is not { IsValid: true } || displayWidth <= 0 || displayHeight <= 0 || targetRatio <= 0)
        {
            CompositionSelectionCanvas.Visibility = Visibility.Collapsed;
            return false;
        }
        CompositionSelectionCanvas.Visibility = Visibility.Visible;
        var sourceWidth = sourceSize.Width;
        var sourceHeight = sourceSize.Height;
        var zoom = Math.Max(1, Math.Clamp(composition.Zoom, 0.5, 3));
        var coverScale = Math.Max(targetRatio / sourceWidth, 1 / sourceHeight) * zoom;
        var cropWidth = Math.Min(sourceWidth, targetRatio / coverScale);
        var cropHeight = Math.Min(sourceHeight, 1 / coverScale);
        var positionX = Math.Clamp(composition.FocusX * 100 + composition.OffsetX * 25, 0, 100) / 100;
        var positionY = Math.Clamp(composition.FocusY * 100 + composition.OffsetY * 25, 0, 100) / 100;
        var displayScale = Math.Min(displayWidth / sourceWidth, displayHeight / sourceHeight);
        var imageLeft = (displayWidth - sourceWidth * displayScale) / 2;
        var imageTop = (displayHeight - sourceHeight * displayScale) / 2;
        _selectionWidth = cropWidth * displayScale;
        _selectionHeight = cropHeight * displayScale;
        _selectionLeft = imageLeft + (sourceWidth - cropWidth) * positionX * displayScale;
        _selectionTop = imageTop + (sourceHeight - cropHeight) * positionY * displayScale;

        Canvas.SetLeft(CompositionSelectionFrame, _selectionLeft);
        Canvas.SetTop(CompositionSelectionFrame, _selectionTop);
        CompositionSelectionFrame.Width = _selectionWidth;
        CompositionSelectionFrame.Height = _selectionHeight;
        SetCanvasRect(CompositionShadeTop, 0, 0, displayWidth, _selectionTop);
        SetCanvasRect(CompositionShadeBottom, 0, _selectionTop + _selectionHeight, displayWidth,
            Math.Max(0, displayHeight - _selectionTop - _selectionHeight));
        SetCanvasRect(CompositionShadeLeft, 0, _selectionTop, _selectionLeft, _selectionHeight);
        SetCanvasRect(CompositionShadeRight, _selectionLeft + _selectionWidth, _selectionTop,
            Math.Max(0, displayWidth - _selectionLeft - _selectionWidth), _selectionHeight);
        Canvas.SetLeft(CompositionFocusMarker, _selectionLeft + _selectionWidth / 2 - CompositionFocusMarker.Width / 2);
        Canvas.SetTop(CompositionFocusMarker, _selectionTop + _selectionHeight / 2 - CompositionFocusMarker.Height / 2);
        return true;
    }

    private static void SetCanvasRect(FrameworkElement element, double left, double top, double width, double height)
    {
        Canvas.SetLeft(element, left);
        Canvas.SetTop(element, top);
        element.Width = Math.Max(0, width);
        element.Height = Math.Max(0, height);
    }

    private void ApplyCompositionToImage(Image image, ThemeComposition composition, double width, double height)
    {
        if (!TryCalculateCompositionLayout(image, composition, width, height, out var renderedWidth, out var renderedHeight)) return;
        var positionX = Math.Clamp(composition.FocusX * 100 + composition.OffsetX * 25, 0, 100) / 100;
        var positionY = Math.Clamp(composition.FocusY * 100 + composition.OffsetY * 25, 0, 100) / 100;
        image.Width = renderedWidth;
        image.Height = renderedHeight;
        image.HorizontalAlignment = HorizontalAlignment.Left;
        image.VerticalAlignment = VerticalAlignment.Top;
        image.Stretch = Stretch.Fill;
        image.RenderTransform = null;
        Canvas.SetLeft(image, (width - renderedWidth) * positionX);
        Canvas.SetTop(image, (height - renderedHeight) * positionY);
    }

    private bool TryCalculateCompositionLayout(
        Image image,
        ThemeComposition composition,
        double width,
        double height,
        out double renderedWidth,
        out double renderedHeight)
    {
        renderedWidth = 0;
        renderedHeight = 0;
        if (width <= 0 || height <= 0 || image.Source is null) return false;
        var sourceSize = _sourceImageSizes.TryGetValue(image.Source, out var decodedSize)
            ? decodedSize
            : image.Source is BitmapImage bitmap && bitmap.PixelWidth > 0 && bitmap.PixelHeight > 0
                ? new ThemeRegionSize(bitmap.PixelWidth, bitmap.PixelHeight)
                : null;
        if (sourceSize is not { IsValid: true }) return false;
        var fit = composition.Fit is "fill" or "contain" ? composition.Fit : "cover";
        var zoom = Math.Clamp(composition.Zoom, 0.5, 3);
        if (fit == "cover") zoom = Math.Max(1, zoom);
        if (fit == "fill")
        {
            renderedWidth = width * zoom;
            renderedHeight = height * zoom;
        }
        else
        {
            var baseScale = fit == "contain"
                ? Math.Min(width / sourceSize.Width, height / sourceSize.Height)
                : Math.Max(width / sourceSize.Width, height / sourceSize.Height);
            renderedWidth = sourceSize.Width * baseScale * zoom;
            renderedHeight = sourceSize.Height * baseScale * zoom;
        }
        return renderedWidth > 0 && renderedHeight > 0;
    }

    private void CopyBackgroundComposition_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTheme?.IsBundled != false || _currentDraft is null || _compositionSlot == ThemeImageSlot.Background) return;
        ApplyCompositionDraft(SetComposition(_currentDraft, _compositionSlot, _currentDraft.BackgroundComposition), "已复制主背景构图参数");
    }

    private void CropToFrame_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTheme?.IsBundled != false || _currentDraft is null) return;
        var current = GetComposition(_currentDraft, _compositionSlot);
        var next = current with { Fit = "cover", Zoom = 1, OffsetX = 0, OffsetY = 0 };
        ApplyCompositionDraft(SetComposition(_currentDraft, _compositionSlot, next), IsChinese ? "已适配当前区域框" : "Fitted to the current region frame");
    }

    private void ResetComposition_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTheme?.IsBundled != false || _currentDraft is null) return;
        ApplyCompositionDraft(SetComposition(_currentDraft, _compositionSlot, ThemeComposition.Recommended(_compositionSlot)), "已恢复推荐构图");
    }

    private void ApplyCompositionDraft(ThemeDraft next, string message)
    {
        if (_currentDraft is null || next == _currentDraft) return;
        _undo.Push(_currentDraft);
        _currentDraft = next;
        _redo.Clear();
        ApplyDraftToEditor(next);
        UpdateEditorState();
        ShowMessage(message, "保存主题后正式保留，可使用撤销恢复。", InfoBarSeverity.Success);
    }

    private void PreviewDarkModeToggle_Toggled(object sender, RoutedEventArgs e) => UpdateAppearancePreview();

    private void UpdateAppearancePreview()
    {
        if (_currentDraft is null) return;
        var dark = PreviewDarkModeToggle.IsOn;
        var surfaceColor = dark ? Color.FromArgb(255, 5, 19, 33) : Color.FromArgb(255, 249, 252, 251);
        var textColor = dark ? Color.FromArgb(255, 244, 251, 255) : Color.FromArgb(255, 11, 49, 89);
        var opacities = dark
            ? new[] { _currentDraft.DarkSidebarOpacity, _currentDraft.DarkCardOpacity, _currentDraft.DarkComposerOpacity, _currentDraft.DarkPageOpacity }
            : new[] { _currentDraft.LightSidebarOpacity, _currentDraft.LightCardOpacity, _currentDraft.LightComposerOpacity, _currentDraft.LightPageOpacity };
        opacities = [.. opacities, dark ? _currentDraft.DarkComposerOpacity : _currentDraft.LightComposerOpacity, dark ? _currentDraft.DarkCardOpacity : _currentDraft.LightCardOpacity];
        foreach (var text in new[] { SidebarModePreviewText, MessageModePreviewText, ComposerModePreviewText, HomeModePreviewText, HomeComposerModePreviewText, PolaroidModePreviewText })
            text.Foreground = new SolidColorBrush(textColor);
        SidebarModePreviewOverlay.Background = CreatePreviewGradient(true, surfaceColor,
            (0, opacities[0]), (1, opacities[0] * (dark ? 0.757 : 0.724)));
        MessageModePreviewOverlay.Background = CreatePreviewGradient(true, surfaceColor,
            (0, opacities[1]), (1, opacities[1] * 0.333));
        var composerMultipliers = dark ? new[] { 1.0, 0.710, 0.452, 0.226 } : new[] { 1.0, 0.667, 0.375, 0.125 };
        ComposerModePreviewOverlay.Background = CreatePreviewGradient(false, surfaceColor,
            (0, opacities[2] * composerMultipliers[0]), (0.54, opacities[2] * composerMultipliers[1]),
            (0.76, opacities[2] * composerMultipliers[2]), (1, opacities[2] * composerMultipliers[3]));
        HomeComposerModePreviewOverlay.Background = CreatePreviewGradient(false, surfaceColor,
            (0, opacities[4] * composerMultipliers[0]), (0.54, opacities[4] * composerMultipliers[1]),
            (0.76, opacities[4] * composerMultipliers[2]), (1, opacities[4] * composerMultipliers[3]));
        var heroBlue = dark ? Color.FromArgb(255, 5, 19, 33) : Color.FromArgb(255, 7, 39, 86);
        HomeModePreviewOverlay.Background = CreatePreviewGradient(false, heroBlue,
            (0, 0.98), (0.31, 0.96), (0.45, 0.76), (0.64, 0), (1, 0));
        PolaroidModePreviewOverlay.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        var white = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
        HomeModePreviewText.Foreground = white;
        PolaroidModePreviewText.Foreground = white;
        var gridWidth = AppearancePreviewGrid.ActualWidth > 0 ? AppearancePreviewGrid.ActualWidth : Math.Max(260, InspectorHost.ActualWidth - 28);
        var columnWidth = Math.Max(120, (gridWidth - AppearancePreviewGrid.ColumnSpacing) / 2 - 8);
        var sidebarSize = SizeAppearancePreview(SidebarModePreviewSurface, ThemeImageSlot.Sidebar, Math.Min(110, columnWidth), 190);
        var messageSize = SizeAppearancePreview(MessageModePreviewSurface, ThemeImageSlot.Background, columnWidth, Math.Min(420, columnWidth));
        var composerSize = SizeAppearancePreview(ComposerModePreviewSurface, ThemeImageSlot.Composer, columnWidth, 120);
        var homeSize = SizeAppearancePreview(HomeModePreviewSurface, ThemeImageSlot.Home, columnWidth, 120);
        var homeComposerSize = SizeAppearancePreview(HomeComposerModePreviewSurface, ThemeImageSlot.HomeComposer, columnWidth, 120);
        var polaroidSize = SizeAppearancePreview(PolaroidModePreviewSurface, ThemeImageSlot.Polaroid, Math.Min(90, columnWidth), 107);
        ApplyCompositionToImage(SidebarModePreviewImage, _currentDraft.SidebarComposition, sidebarSize.Width, sidebarSize.Height);
        ApplyCompositionToImage(MessageModePreviewImage, _currentDraft.BackgroundComposition, messageSize.Width, messageSize.Height);
        ApplyCompositionToImage(ComposerModePreviewImage, _currentDraft.ComposerComposition, composerSize.Width, composerSize.Height);
        ApplyCompositionToImage(HomeModePreviewImage, _currentDraft.HomeComposition, homeSize.Width, homeSize.Height);
        ApplyCompositionToImage(HomeComposerModePreviewImage, _currentDraft.HomeComposerComposition, homeComposerSize.Width, homeComposerSize.Height);
        ApplyCompositionToImage(PolaroidModePreviewImage, _currentDraft.PolaroidComposition, polaroidSize.Width, polaroidSize.Height);
        UpdateContrastWarning(dark, opacities);
    }

    private static LinearGradientBrush CreatePreviewGradient(bool vertical, Color color, params (double Offset, double Opacity)[] stops)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0),
            EndPoint = vertical ? new Windows.Foundation.Point(0, 1) : new Windows.Foundation.Point(1, 0)
        };
        foreach (var (offset, opacity) in stops)
            brush.GradientStops.Add(new GradientStop
            {
                Offset = Math.Clamp(offset, 0, 1),
                Color = Color.FromArgb((byte)Math.Round(Math.Clamp(opacity, 0, 1) * 255), color.R, color.G, color.B)
            });
        return brush;
    }

    private ThemeRegionSize SizeAppearancePreview(FrameworkElement surface, ThemeImageSlot slot, double maxWidth, double maxHeight)
    {
        var fallback = slot switch
        {
            ThemeImageSlot.Sidebar => new ThemeRegionSize(275, 998),
            ThemeImageSlot.Composer => new ThemeRegionSize(736, 98),
            ThemeImageSlot.Home => new ThemeRegionSize(1603, 215),
            ThemeImageSlot.HomeComposer => new ThemeRegionSize(920, 98),
            ThemeImageSlot.Polaroid => new ThemeRegionSize(122, 158),
            _ => new ThemeRegionSize(1647, 998)
        };
        var measured = _regionMetrics?.Get(slot);
        var region = measured is { IsValid: true } ? measured : fallback;
        var scale = Math.Min(maxWidth / region.Width, maxHeight / region.Height);
        var result = new ThemeRegionSize(Math.Max(1, region.Width * scale), Math.Max(1, region.Height * scale));
        surface.Width = result.Width;
        surface.Height = result.Height;
        return result;
    }

    private void UpdateContrastWarning(bool dark, IReadOnlyList<double> opacities)
    {
        var slots = new[] { ThemeImageSlot.Sidebar, ThemeImageSlot.Background, ThemeImageSlot.Composer, ThemeImageSlot.Home, ThemeImageSlot.HomeComposer, ThemeImageSlot.Polaroid };
        var labels = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh"
            ? new[] { "侧边栏", "消息区", "任务输入框", "首页照片框", "首页输入框", "宝丽来照片框" }
            : new[] { "Sidebar", "Messages", "Task composer", "Home frame", "Home composer", "Polaroid frame" };
        var textLuminance = dark ? 0.93 : 0.03;
        var surfaceLuminance = dark ? 0.01 : 0.96;
        var warnings = new List<string>();
        for (var index = 0; index < slots.Length; index++)
        {
            var imageLuminance = _imageAnalyses.TryGetValue(slots[index], out var analysis) ? analysis.AverageLuminance : 0.5;
            var composite = imageLuminance * (1 - opacities[index]) + surfaceLuminance * opacities[index];
            var contrast = (Math.Max(composite, textLuminance) + 0.05) / (Math.Min(composite, textLuminance) + 0.05);
            if (contrast < 4.5) warnings.Add($"{labels[index]} {contrast:F1}:1");
        }
        if (opacities.Min() < 0.12) warnings.Add(System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh" ? "遮罩过透明" : "veil too transparent");
        PreviewContrastInfoBar.IsOpen = true;
        PreviewContrastInfoBar.Severity = warnings.Count == 0 ? InfoBarSeverity.Success : InfoBarSeverity.Warning;
        PreviewContrastInfoBar.Title = warnings.Count == 0
            ? (System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh" ? "文字对比度通过" : "Text contrast passes")
            : (System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh" ? "建议提高可读性" : "Readability adjustment recommended");
        PreviewContrastInfoBar.Message = warnings.Count == 0
            ? (System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh" ? "六个区域的估算对比度均达到 4.5:1。" : "All six estimated contrast ratios reach 4.5:1.")
            : string.Join(" · ", warnings);
    }

    private async Task RefreshImageAnalysesAsync(ThemeDraft draft, string directory)
    {
        var generation = ++_analysisGeneration;
        var paths = new Dictionary<ThemeImageSlot, string>
        {
            [ThemeImageSlot.Background] = Path.Combine(directory, draft.BackgroundImageFileName),
            [ThemeImageSlot.Sidebar] = Path.Combine(directory, draft.SidebarImageFileName),
            [ThemeImageSlot.Composer] = Path.Combine(directory, draft.ComposerImageFileName),
            [ThemeImageSlot.Home] = Path.Combine(directory, draft.HomeImageFileName),
            [ThemeImageSlot.HomeComposer] = Path.Combine(directory, draft.HomeComposerImageFileName),
            [ThemeImageSlot.Polaroid] = Path.Combine(directory, draft.PolaroidImageFileName)
        };
        try
        {
            var byPath = paths.Values.Distinct(StringComparer.OrdinalIgnoreCase)
                .ToDictionary(path => path, path => _paletteService.AnalyzeAsync(path), StringComparer.OrdinalIgnoreCase);
            await Task.WhenAll(byPath.Values);
            if (generation != _analysisGeneration) return;
            _imageAnalyses.Clear();
            foreach (var (slot, path) in paths) _imageAnalyses[slot] = await byPath[path];
            UpdateAppearancePreview();
        }
        catch (Exception error)
        {
            if (generation == _analysisGeneration)
                PaletteAnalysisText.Text = $"图片分析暂不可用：{error.Message}";
        }
    }

    private async void ApplyPalette_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTheme?.IsBundled != false || _currentDraft is null || sender is not Button button) return;
        try
        {
            SetBusy(true);
            var path = Path.Combine(_selectedTheme.DirectoryPath, GetImageFileName(_currentDraft, _compositionSlot));
            var analysis = await _paletteService.AnalyzeAsync(path);
            _imageAnalyses[_compositionSlot] = analysis;
            var suggestion = ThemePaletteService.CreateSuggestion(analysis, button.Tag?.ToString() ?? "fresh");
            var next = _currentDraft with
            {
                Accent = suggestion.Accent,
                LightPageOpacity = suggestion.LightPage,
                LightSidebarOpacity = suggestion.LightSidebar,
                LightComposerOpacity = suggestion.LightComposer,
                LightCardOpacity = suggestion.LightCard,
                DarkPageOpacity = suggestion.DarkPage,
                DarkSidebarOpacity = suggestion.DarkSidebar,
                DarkComposerOpacity = suggestion.DarkComposer,
                DarkCardOpacity = suggestion.DarkCard
            };
            _undo.Push(_currentDraft);
            _currentDraft = next;
            _redo.Clear();
            ApplyDraftToEditor(next);
            PaletteAnalysisText.Text = $"主色 {analysis.DominantHex} · 强调色 {analysis.AccentHex} · 亮度 {analysis.AverageLuminance:P0} · 肤色样本 {analysis.SkinPixelRatio:P0}（已避开）";
            UpdateEditorState();
            ShowMessage("自动配色已加入草稿", "强调色和浅色/深色玻璃参数已根据当前区域图片生成。", InfoBarSeverity.Success);
        }
        catch (Exception error) { ShowMessage("无法生成自动配色", error.Message, InfoBarSeverity.Error); }
        finally { SetBusy(false); }
    }

    private ThemeDefinition BuildDraftTheme()
    {
        var source = _selectedTheme ?? throw new InvalidOperationException("尚未选择主题。");
        var draft = _currentDraft ?? ThemeDraft.FromTheme(source);
        var value = new ThemeDefinition
        {
            Id = source.Id,
            Name = draft.Name,
            DirectoryPath = source.DirectoryPath,
            ImageFileName = draft.BackgroundImageFileName,
            SidebarImageFileName = draft.SidebarImageFileName,
            ComposerImageFileName = draft.ComposerImageFileName,
            HomeImageFileName = draft.HomeImageFileName,
            HomeComposerImageFileName = draft.HomeComposerImageFileName,
            PolaroidImageFileName = draft.PolaroidImageFileName,
            IsBundled = source.IsBundled,
            IsActive = source.IsActive,
            BackgroundComposition = draft.BackgroundComposition,
            SidebarComposition = draft.SidebarComposition,
            ComposerComposition = draft.ComposerComposition,
            HomeComposition = draft.HomeComposition,
            HomeComposerComposition = draft.HomeComposerComposition,
            PolaroidComposition = draft.PolaroidComposition
        };
        ApplyDraftToTheme(draft, value);
        return value;
    }

    private static void ApplyDraftToTheme(ThemeDraft draft, ThemeDefinition theme)
    {
        theme.Name = draft.Name;
        theme.ImageFileName = draft.BackgroundImageFileName;
        theme.SidebarImageFileName = draft.SidebarImageFileName;
        theme.ComposerImageFileName = draft.ComposerImageFileName;
        theme.HomeImageFileName = draft.HomeImageFileName;
        theme.HomeComposerImageFileName = draft.HomeComposerImageFileName;
        theme.PolaroidImageFileName = draft.PolaroidImageFileName;
        theme.Appearance = draft.Appearance;
        theme.SafeArea = draft.SafeArea;
        theme.TaskMode = draft.TaskMode;
        theme.Accent = draft.Accent;
        theme.BackgroundComposition = draft.BackgroundComposition;
        theme.SidebarComposition = draft.SidebarComposition;
        theme.ComposerComposition = draft.ComposerComposition;
        theme.HomeComposition = draft.HomeComposition;
        theme.HomeComposerComposition = draft.HomeComposerComposition;
        theme.PolaroidComposition = draft.PolaroidComposition;
        theme.FocusX = draft.BackgroundComposition.FocusX;
        theme.FocusY = draft.BackgroundComposition.FocusY;
        theme.LightPageOpacity = draft.LightPageOpacity;
        theme.LightSidebarOpacity = draft.LightSidebarOpacity;
        theme.LightComposerOpacity = draft.LightComposerOpacity;
        theme.LightCardOpacity = draft.LightCardOpacity;
        theme.DarkPageOpacity = draft.DarkPageOpacity;
        theme.DarkSidebarOpacity = draft.DarkSidebarOpacity;
        theme.DarkComposerOpacity = draft.DarkComposerOpacity;
        theme.DarkCardOpacity = draft.DarkCardOpacity;
        theme.ComponentMaterials = draft.ComponentMaterials;
    }

    private static string GetTag(ComboBox comboBox, string fallback) =>
        (comboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? fallback;

    private void UpdateEditorState()
    {
        var editable = _selectedTheme?.IsBundled == false;
        DraftStatusText.Text = _selectedTheme is null
            ? string.Empty
            : _isLiveSyncing ? (IsChinese ? "● 正在实时同步" : "● Live syncing")
            : _isPreviewing ? "● 正在临时预览"
            : IsDirty ? "● 有未保存的更改"
            : _selectedTheme.IsActive ? "✓ 已保存并正在使用"
            : "✓ 已保存";
        UndoButton.IsEnabled = !_isBusy && editable && _undo.Count > 0;
        RedoButton.IsEnabled = !_isBusy && editable && _redo.Count > 0;
        SaveButton.IsEnabled = !_isBusy && editable && IsDirty;
        LivePreviewToggle.IsEnabled = !_isBusy && editable;
        PreviewButton.IsEnabled = !_isBusy && _selectedTheme is not null && !LivePreviewToggle.IsOn;
        ActivateButton.IsEnabled = !_isBusy && _selectedTheme is not null && !IsDirty;
        CancelPreviewButton.IsEnabled = !_isBusy && _isPreviewing;
        DuplicateButton.IsEnabled = !_isBusy && _selectedTheme is not null;
        ExportButton.IsEnabled = !_isBusy && _selectedTheme is not null && !IsDirty;
        HistoryButton.IsEnabled = !_isBusy && editable && !IsDirty && !_isPreviewing;
        DeleteButton.IsEnabled = !_isBusy && editable && !_isPreviewing;
        CopyBackgroundCompositionButton.IsEnabled = !_isBusy && editable && _currentDraft is not null
            && _compositionSlot != ThemeImageSlot.Background
            && GetComposition(_currentDraft, _compositionSlot) != _currentDraft.BackgroundComposition;
        ResetCompositionButton.IsEnabled = !_isBusy && editable && _currentDraft is not null
            && GetComposition(_currentDraft, _compositionSlot) != ThemeComposition.Recommended(_compositionSlot);
        CropToFrameButton.IsEnabled = !_isBusy && editable && _currentDraft is not null;
        ResetComponentMaterialButton.IsEnabled = !_isBusy && editable && _currentDraft is not null
            && _currentDraft.ComponentMaterials.Get(_componentSlot) != ThemeComponentMaterials.Default.Get(_componentSlot);
        FreshPaletteButton.IsEnabled = !_isBusy && editable;
        MidnightPaletteButton.IsEnabled = !_isBusy && editable;
        LowFogPaletteButton.IsEnabled = !_isBusy && editable;
        if (_currentDraft is not null)
        {
            SidebarResetButton.IsEnabled = !_isBusy && editable && _currentDraft.SidebarImageFileName != _currentDraft.BackgroundImageFileName;
            ComposerResetButton.IsEnabled = !_isBusy && editable && _currentDraft.ComposerImageFileName != _currentDraft.BackgroundImageFileName;
            HomeResetButton.IsEnabled = !_isBusy && editable && _currentDraft.HomeImageFileName != _currentDraft.BackgroundImageFileName;
            HomeComposerResetButton.IsEnabled = !_isBusy && editable && _currentDraft.HomeComposerImageFileName != _currentDraft.ComposerImageFileName;
            PolaroidResetButton.IsEnabled = !_isBusy && editable && _currentDraft.PolaroidImageFileName != _currentDraft.HomeImageFileName;
        }
    }

    private bool IsDirty => _currentDraft is not null && _savedDraft is not null && _currentDraft != _savedDraft;

    private async Task<bool> ConfirmDiscardAsync()
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "放弃未保存的更改？",
            Content = "切换主题会丢弃当前草稿。你也可以先取消并保存。",
            PrimaryButtonText = "放弃更改",
            CloseButtonText = "继续编辑",
            DefaultButton = ContentDialogButton.Close
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private void SyncColorPicker(string value)
    {
        if (!TryParseColor(value, out var color)) return;
        var wasLoading = _isLoadingEditor;
        _isLoadingEditor = true;
        AccentColorPicker.Color = color;
        _isLoadingEditor = wasLoading;
    }

    private static bool TryParseColor(string value, out Color color)
    {
        color = Color.FromArgb(255, 21, 87, 176);
        var hex = value.Trim().TrimStart('#');
        if (hex.Length is not (6 or 8) || !uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var number)) return false;
        var red = (byte)(number >> (hex.Length == 8 ? 24 : 16));
        var green = (byte)(number >> (hex.Length == 8 ? 16 : 8));
        var blue = (byte)(number >> (hex.Length == 8 ? 8 : 0));
        var alpha = hex.Length == 8 ? (byte)number : (byte)255;
        color = Color.FromArgb(alpha, red, green, blue);
        return true;
    }

    private void UpdateFocusMarker()
    {
        if (PreviewSurface.ActualWidth <= 0 || PreviewSurface.ActualHeight <= 0) return;
        var composition = _currentDraft is null ? ThemeComposition.Recommended(_previewSlot) : GetComposition(_currentDraft, _previewSlot);
        Canvas.SetLeft(FocusMarker, Math.Clamp(composition.FocusX * PreviewSurface.ActualWidth - FocusMarker.Width / 2, 0, PreviewSurface.ActualWidth - FocusMarker.Width));
        Canvas.SetTop(FocusMarker, Math.Clamp(composition.FocusY * PreviewSurface.ActualHeight - FocusMarker.Height / 2, 0, PreviewSurface.ActualHeight - FocusMarker.Height));
    }

    private void SetEditorEnabled(bool enabled)
    {
        NameTextBox.IsEnabled = enabled;
        AppearanceComboBox.IsEnabled = enabled;
        SafeAreaComboBox.IsEnabled = enabled;
        TaskModeComboBox.IsEnabled = enabled;
        AccentTextBox.IsEnabled = enabled;
        AccentColorPicker.IsEnabled = enabled;
        CompositionSlotComboBox.IsEnabled = true;
        CompositionFocusXSlider.IsEnabled = enabled;
        CompositionFocusYSlider.IsEnabled = enabled;
        CompositionZoomSlider.IsEnabled = enabled;
        CompositionFitComboBox.IsEnabled = enabled;
        CompositionOffsetXSlider.IsEnabled = enabled;
        CompositionOffsetYSlider.IsEnabled = enabled;
        CompositionPreviewSurface.IsTabStop = enabled;
        CropToFrameButton.IsEnabled = enabled;
        LightPageOpacitySlider.IsEnabled = enabled;
        LightSidebarOpacitySlider.IsEnabled = enabled;
        LightComposerOpacitySlider.IsEnabled = enabled;
        LightCardOpacitySlider.IsEnabled = enabled;
        DarkPageOpacitySlider.IsEnabled = enabled;
        DarkSidebarOpacitySlider.IsEnabled = enabled;
        DarkComposerOpacitySlider.IsEnabled = enabled;
        DarkCardOpacitySlider.IsEnabled = enabled;
        ComponentMaterialSlotComboBox.IsEnabled = true;
        LightComponentColorTextBox.IsEnabled = enabled;
        LightComponentOpacitySlider.IsEnabled = enabled;
        DarkComponentColorTextBox.IsEnabled = enabled;
        DarkComponentOpacitySlider.IsEnabled = enabled;
        ResetComponentMaterialButton.IsEnabled = enabled;
        BackgroundImageButton.IsEnabled = enabled;
        SidebarImageButton.IsEnabled = enabled;
        ComposerImageButton.IsEnabled = enabled;
        HomeImageButton.IsEnabled = enabled;
        HomeComposerImageButton.IsEnabled = enabled;
        PolaroidImageButton.IsEnabled = enabled;
    }

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        ImportButton.IsEnabled = !busy;
        ImportPackageButton.IsEnabled = !busy;
        ThemesList.IsEnabled = !busy;
        UpdateEditorState();
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
        var compactMaterials = e.NewSize.Width < 900;
        MaterialsGrid.ColumnDefinitions[1].Width = compactMaterials ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        ComponentMaterialsGrid.ColumnDefinitions[1].Width = compactMaterials ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        Grid.SetColumn(DarkMaterialsPanel, compactMaterials ? 0 : 1);
        Grid.SetRow(DarkMaterialsPanel, compactMaterials ? 1 : 0);
        Grid.SetColumn(DarkComponentMaterialPanel, compactMaterials ? 0 : 1);
        Grid.SetRow(DarkComponentMaterialPanel, compactMaterials ? 1 : 0);
        var compactImages = e.NewSize.Width < 1180;
        ImagesGrid.ColumnDefinitions[2].Width = compactImages ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        ImagesGrid.ColumnDefinitions[3].Width = compactImages ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        ImagesGrid.ColumnDefinitions[4].Width = compactImages ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        ImagesGrid.ColumnDefinitions[5].Width = compactImages ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        var imageButtons = new[] { BackgroundImageButton, SidebarImageButton, ComposerImageButton, HomeImageButton, HomeComposerImageButton, PolaroidImageButton };
        for (var index = 0; index < imageButtons.Length; index++)
        {
            Grid.SetColumn(imageButtons[index], compactImages ? index % 2 : index);
            Grid.SetRow(imageButtons[index], compactImages ? (index / 2) * 2 : 0);
        }
        Grid.SetColumn(SidebarResetButton, compactImages ? 1 : 1);
        Grid.SetRow(SidebarResetButton, 1);
        Grid.SetColumn(ComposerResetButton, compactImages ? 0 : 2);
        Grid.SetRow(ComposerResetButton, compactImages ? 3 : 1);
        Grid.SetColumn(HomeResetButton, compactImages ? 1 : 3);
        Grid.SetRow(HomeResetButton, compactImages ? 3 : 1);
        Grid.SetColumn(HomeComposerResetButton, compactImages ? 0 : 4);
        Grid.SetRow(HomeComposerResetButton, compactImages ? 5 : 1);
        Grid.SetColumn(PolaroidResetButton, compactImages ? 1 : 5);
        Grid.SetRow(PolaroidResetButton, compactImages ? 5 : 1);
        var previewCards = new[] { SidebarModePreview, MessageModePreview, ComposerModePreview, HomeModePreview, HomeComposerModePreview, PolaroidModePreview };
        for (var index = 0; index < previewCards.Length; index++)
        {
            Grid.SetColumn(previewCards[index], index % 2);
            Grid.SetRow(previewCards[index], index / 2);
        }
        UpdateCompositionPreview();
        UpdateAppearancePreview();
    }

    private sealed record ThemeDraft(
        string BackgroundImageFileName,
        string SidebarImageFileName,
        string ComposerImageFileName,
        string HomeImageFileName,
        string HomeComposerImageFileName,
        string PolaroidImageFileName,
        ThemeComposition BackgroundComposition,
        ThemeComposition SidebarComposition,
        ThemeComposition ComposerComposition,
        ThemeComposition HomeComposition,
        ThemeComposition HomeComposerComposition,
        ThemeComposition PolaroidComposition,
        string Name,
        string Appearance,
        string SafeArea,
        string TaskMode,
        string Accent,
        double LightPageOpacity,
        double LightSidebarOpacity,
        double LightComposerOpacity,
        double LightCardOpacity,
        double DarkPageOpacity,
        double DarkSidebarOpacity,
        double DarkComposerOpacity,
        double DarkCardOpacity,
        ThemeComponentMaterials ComponentMaterials)
    {
        public static ThemeDraft FromTheme(ThemeDefinition theme) => new(
            theme.ImageFileName,
            theme.EffectiveSidebarImageFileName,
            theme.EffectiveComposerImageFileName,
            theme.EffectiveHomeImageFileName,
            theme.EffectiveHomeComposerImageFileName,
            theme.EffectivePolaroidImageFileName,
            theme.BackgroundComposition,
            theme.SidebarComposition,
            theme.ComposerComposition,
            theme.HomeComposition,
            theme.HomeComposerComposition,
            theme.PolaroidComposition,
            theme.Name,
            theme.Appearance,
            theme.SafeArea,
            theme.TaskMode,
            theme.Accent,
            theme.LightPageOpacity,
            theme.LightSidebarOpacity,
            theme.LightComposerOpacity,
            theme.LightCardOpacity,
            theme.DarkPageOpacity,
            theme.DarkSidebarOpacity,
            theme.DarkComposerOpacity,
            theme.DarkCardOpacity,
            theme.ComponentMaterials);
    }
}
