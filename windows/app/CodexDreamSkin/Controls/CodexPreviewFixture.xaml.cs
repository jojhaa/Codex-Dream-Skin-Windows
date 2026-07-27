using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Windows.Foundation;

namespace CodexDreamSkin.Controls;

public sealed partial class CodexPreviewFixture : UserControl
{
    private const double DesignWidth = 1922;
    private const double DesignHeight = 1034;

    public CodexPreviewFixture()
    {
        InitializeComponent();
        ShowScene("home");
    }

    public string ThemeName
    {
        get => PreviewName.Text;
        set => PreviewName.Text = value;
    }

    public string Status
    {
        get => PreviewStatus.Text;
        set => PreviewStatus.Text = value;
    }

    public ImageSource? BackgroundSource
    {
        get => PreviewBackgroundImage.Source;
        set => PreviewBackgroundImage.Source = value;
    }

    public ImageSource? SidebarSource
    {
        get => PreviewSidebarImage.Source;
        set => PreviewSidebarImage.Source = value;
    }

    public bool UseContinuousSidebarBackground
    {
        set
        {
            PreviewSidebarImage.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
            PreviewSidebarMaterial.Background = PreviewMaterial(value, 0xC8, 0x08, 0x20, 0x37);
            PreviewTaskSidebarMaterial.Background = PreviewMaterial(value, 0xC8, 0x0A, 0x22, 0x39);
            PreviewSettingsSidebarMaterial.Background = PreviewMaterial(value, 0xD2, 0x0A, 0x22, 0x39);
        }
    }

    public bool MatchWorkspaceTransparency
    {
        set
        {
            PreviewHomeHeaderMaterial.Background = PreviewMaterial(value, 0xD0, 0x0A, 0x20, 0x36);
            PreviewHomeWorkspaceMaterial.Background = PreviewMaterial(value, 0x2B, 0x04, 0x18, 0x2B);
            PreviewTaskHeaderMaterial.Background = PreviewMaterial(value, 0xB7, 0x08, 0x1E, 0x34);
        }
    }

    private static SolidColorBrush PreviewMaterial(bool transparent, byte alpha, byte red, byte green, byte blue) =>
        new(transparent ? Color.FromArgb(0x00, 0x00, 0x00, 0x00) : Color.FromArgb(alpha, red, green, blue));

    public ImageSource? PolaroidSource
    {
        get => PreviewPolaroidImage.Source;
        set => PreviewPolaroidImage.Source = value;
    }

    public void ApplyDecorationProfile(string profile)
    {
        var isMilkyWay = string.Equals(profile, "milky-way", StringComparison.Ordinal);
        var isMinimal = string.Equals(profile, "minimal", StringComparison.Ordinal);
        var isDecorated = !isMilkyWay && !isMinimal;
        PreviewChromeSignature.Text = isDecorated ? "Kanna / 璟奈" : string.Empty;
        PreviewHomeSignature.Text = PreviewChromeSignature.Text;
        PreviewChromeSignature.Visibility = isDecorated ? Visibility.Visible : Visibility.Collapsed;
        PreviewHomeSignature.Visibility = isDecorated ? Visibility.Visible : Visibility.Collapsed;
        PreviewDecorationRibbonText.Text = "01  BLUE MOMENT";
        PreviewPolaroidCaptionText.Text = "BLUE MOMENT · 01";
        PreviewDecorationRibbon.Visibility = isDecorated ? Visibility.Visible : Visibility.Collapsed;
        PreviewPolaroidCard.Visibility = isDecorated ? Visibility.Visible : Visibility.Collapsed;
        PreviewDecorationRibbon.Background = new SolidColorBrush(Color.FromArgb(0xD5, 0x23, 0x9B, 0xE1));
        PreviewDecorationRibbon.CornerRadius = new CornerRadius(2);
        PreviewPolaroidCard.Background = new SolidColorBrush(Color.FromArgb(0xF6, 0xFF, 0xFF, 0xFF));
        PreviewPolaroidCard.BorderBrush = new SolidColorBrush(Color.FromArgb(0x33, 0x79, 0xB8, 0xFF));
        PreviewPolaroidCard.CornerRadius = new CornerRadius(0);
        PreviewPolaroidCaptionText.Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x76, 0xB9, 0xE7));
        PreviewPolaroidRotation.Angle = 4;
    }

    public void SetFocus(double normalizedX, double normalizedY)
    {
        Canvas.SetLeft(
            PreviewFocusMarker,
            Math.Clamp(normalizedX, 0, 1) * DesignWidth - PreviewFocusMarker.Width / 2);
        Canvas.SetTop(
            PreviewFocusMarker,
            Math.Clamp(normalizedY, 0, 1) * DesignHeight - PreviewFocusMarker.Height / 2);
    }

    public bool TryNormalizePoint(Point point, out Point normalized)
    {
        var scale = Math.Min(ActualWidth / DesignWidth, ActualHeight / DesignHeight);
        if (scale <= 0)
        {
            normalized = default;
            return false;
        }

        var renderedWidth = DesignWidth * scale;
        var renderedHeight = DesignHeight * scale;
        var left = (ActualWidth - renderedWidth) / 2;
        var top = (ActualHeight - renderedHeight) / 2;
        if (point.X < left || point.X > left + renderedWidth ||
            point.Y < top || point.Y > top + renderedHeight)
        {
            normalized = default;
            return false;
        }

        normalized = new Point(
            (point.X - left) / renderedWidth,
            (point.Y - top) / renderedHeight);
        return true;
    }

    public void ResetInteraction()
    {
        PreviewComposerTextBox.Text = string.Empty;
        TaskComposerTextBox.Text = string.Empty;
        PreviewUserMessageText.Text = string.Empty;
        PreviewConversationResult.Visibility = Visibility.Collapsed;
        ShowScene("home");
    }

    public void ShowScene(string scene)
    {
        PreviewHomeScene.Visibility = scene == "home" ? Visibility.Visible : Visibility.Collapsed;
        PreviewTaskScene.Visibility = scene == "task" ? Visibility.Visible : Visibility.Collapsed;
        PreviewSettingsScene.Visibility = scene == "settings" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SceneButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: not null } button)
        {
            ShowScene(button.Tag.ToString() ?? "home");
        }
    }

    private void SendButton_Click(object sender, RoutedEventArgs e) =>
        SendMessage(PreviewComposerTextBox);

    private void TaskSendButton_Click(object sender, RoutedEventArgs e) =>
        SendMessage(TaskComposerTextBox);

    private void SendMessage(TextBox composer)
    {
        var message = composer.Text.Trim();
        if (message.Length == 0)
        {
            return;
        }

        PreviewUserMessageText.Text = message;
        PreviewConversationResult.Visibility = Visibility.Visible;
        PreviewComposerTextBox.Text = string.Empty;
        TaskComposerTextBox.Text = string.Empty;
        ShowScene("task");
    }
}
