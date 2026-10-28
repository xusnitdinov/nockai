using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using NockAI.Services;

namespace NockAI;

public partial class SettingsWindow : Window
{
    private bool _loading = true;
    private DispatcherTimer? _savedFade;

    public SettingsWindow()
    {
        InitializeComponent();
        LoadFrom(SettingsStore.Current.Clone());
        _loading = false;
        ShowPage("General");
    }

    private void LoadFrom(AppSettings s)
    {
        AutoSendCheck.IsChecked = s.AutoSend;
        DelaySlider.Value = s.AutoSendDelayMs;
        DelayValueLabel.Text = $"{s.AutoSendDelayMs} ms";
        HideUnfocusedCheck.IsChecked = s.HideWhenUnfocused;
        StayOnTopCheck.IsChecked = s.StayOnTop;
        SelectAllCheck.IsChecked = s.SelectAllOnOpen;
        ThinkingCheck.IsChecked = s.ShowThinkingAnimation;
        FractionsCheck.IsChecked = s.PreferNiceFractions;
        ProbabilityPctCheck.IsChecked = s.ProbabilityAsPercent;
        CopyCtrlCCheck.IsChecked = s.CopyAnswerWithCtrlC;

        SelectTone(s.AiTone);
        ProfileBox.Text = s.AiProfilePrompt ?? "";
        ChatHistoryCheck.IsChecked = s.EnableChatHistory;
        HistoryTurnsSlider.Value = s.HistoryMessageCount;
        HistoryTurnsLabel.Text = s.HistoryMessageCount.ToString();
        FewShotCheck.IsChecked = s.UseFewShotExamples;
        PreferExplainCheck.IsChecked = s.PreferDetailedExplain;
        TempSlider.Value = s.AiTemperature;
        TempLabel.Text = s.AiTemperature.ToString("0.00");
        MaxTokensSlider.Value = s.AiMaxTokens;
        MaxTokensLabel.Text = s.AiMaxTokens.ToString();
        ExplainWordsSlider.Value = s.ExplainMaxWords;
        ExplainWordsLabel.Text = s.ExplainMaxWords.ToString();

        UpdateDelayEnabled();
        UpdateHistoryEnabled();
    }

    private AppSettings ReadUi() => new()
    {
        AutoSend = AutoSendCheck.IsChecked == true,
        AutoSendDelayMs = (int)Math.Round(DelaySlider.Value),
        HideWhenUnfocused = HideUnfocusedCheck.IsChecked == true,
        StayOnTop = StayOnTopCheck.IsChecked == true,
        SelectAllOnOpen = SelectAllCheck.IsChecked == true,
        ShowThinkingAnimation = ThinkingCheck.IsChecked == true,
        PreferNiceFractions = FractionsCheck.IsChecked == true,
        ProbabilityAsPercent = ProbabilityPctCheck.IsChecked == true,
        CopyAnswerWithCtrlC = CopyCtrlCCheck.IsChecked == true,
        AiTone = ReadTone(),
        AiProfilePrompt = ProfileBox.Text ?? "",
        EnableChatHistory = ChatHistoryCheck.IsChecked == true,
        HistoryMessageCount = (int)Math.Round(HistoryTurnsSlider.Value),
        UseFewShotExamples = FewShotCheck.IsChecked == true,
        PreferDetailedExplain = PreferExplainCheck.IsChecked == true,
        AiTemperature = Math.Round(TempSlider.Value, 2),
        AiMaxTokens = (int)Math.Round(MaxTokensSlider.Value),
        ExplainMaxWords = (int)Math.Round(ExplainWordsSlider.Value)
    };

    private string ReadTone()
    {
        if (ToneCombo.SelectedItem is ComboBoxItem { Tag: string tag } && !string.IsNullOrWhiteSpace(tag))
            return tag;
        return "Chill";
    }

    private void SelectTone(string tone)
    {
        var want = string.IsNullOrWhiteSpace(tone) ? "Chill" : tone.Trim();
        for (var i = 0; i < ToneCombo.Items.Count; i++)
        {
            if (ToneCombo.Items[i] is ComboBoxItem { Tag: string tag } &&
                tag.Equals(want, StringComparison.OrdinalIgnoreCase))
            {
                ToneCombo.SelectedIndex = i;
                return;
            }
        }

        ToneCombo.SelectedIndex = 0;
    }

    private void Persist()
    {
        if (_loading) return;
        SettingsStore.Save(ReadUi());
        FlashSaved();
        UpdateDelayEnabled();
        UpdateHistoryEnabled();
    }

    private void UpdateDelayEnabled()
    {
        var on = AutoSendCheck.IsChecked == true;
        DelayCard.IsEnabled = on;
        DelayCard.Opacity = on ? 1.0 : 0.45;
    }

    private void UpdateHistoryEnabled()
    {
        var on = ChatHistoryCheck.IsChecked == true;
        HistoryTurnsCard.IsEnabled = on;
        HistoryTurnsCard.Opacity = on ? 1.0 : 0.45;
    }

    private void Setting_Changed(object sender, RoutedEventArgs e) => Persist();

    private void ChatHistory_Changed(object sender, RoutedEventArgs e) => Persist();

    private void ProfileBox_LostFocus(object sender, RoutedEventArgs e) => Persist();

    private void ToneCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loading)
            Persist();
    }

    private void DelaySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (DelayValueLabel is null) return;
        DelayValueLabel.Text = $"{(int)Math.Round(e.NewValue)} ms";
        if (!_loading)
            Persist();
    }

    private void HistoryTurnsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (HistoryTurnsLabel is null) return;
        HistoryTurnsLabel.Text = ((int)Math.Round(e.NewValue)).ToString();
        if (!_loading)
            Persist();
    }

    private void TempSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TempLabel is null) return;
        TempLabel.Text = e.NewValue.ToString("0.00");
        if (!_loading)
            Persist();
    }

    private void MaxTokensSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (MaxTokensLabel is null) return;
        MaxTokensLabel.Text = ((int)Math.Round(e.NewValue)).ToString();
        if (!_loading)
            Persist();
    }

    private void ExplainWordsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ExplainWordsLabel is null) return;
        ExplainWordsLabel.Text = ((int)Math.Round(e.NewValue)).ToString();
        if (!_loading)
            Persist();
    }

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string tag })
            ShowPage(tag);
    }

    private void ShowPage(string tag)
    {
        PageGeneral.Visibility = tag == "General" ? Visibility.Visible : Visibility.Collapsed;
        PageOverlay.Visibility = tag == "Overlay" ? Visibility.Visible : Visibility.Collapsed;
        PageAnswers.Visibility = tag == "Answers" ? Visibility.Visible : Visibility.Collapsed;
        PageAi.Visibility = tag == "AI" ? Visibility.Visible : Visibility.Collapsed;
        PageAbout.Visibility = tag == "About" ? Visibility.Visible : Visibility.Collapsed;
        PageTitle.Text = tag switch
        {
            "Overlay" => "Overlay",
            "Answers" => "Answers",
            "AI" => "AI",
            "About" => "About",
            _ => "General"
        };
    }

    private void FlashSaved()
    {
        SavedHint.BeginAnimation(OpacityProperty, null);
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(120));
        SavedHint.BeginAnimation(OpacityProperty, fadeIn);

        _savedFade?.Stop();
        _savedFade = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
        _savedFade.Tick += (_, _) =>
        {
            _savedFade.Stop();
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(350));
            SavedHint.BeginAnimation(OpacityProperty, fadeOut);
        };
        _savedFade.Start();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void TitleDrag_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
            return;
        }

        try { DragMove(); } catch { /* ignore */ }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_loading)
            SettingsStore.Save(ReadUi());
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var dark = 1;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));

        var mica = 2;
        DwmSetWindowAttribute(hwnd, 38, ref mica, sizeof(int));

        var corner = 2;
        DwmSetWindowAttribute(hwnd, 33, ref corner, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
}
