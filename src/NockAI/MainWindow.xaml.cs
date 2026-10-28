using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using NockAI.Services;

namespace NockAI;

public partial class MainWindow : Window
{
    private readonly CalculatorService _calculator;
    private readonly DispatcherTimer _debounce;
    private readonly List<ChatTurn> _history = [];
    private CancellationTokenSource? _evalCts;
    private DispatcherTimer? _footerReset;
    private bool _isVisible;
    private int _evalGeneration;
    private bool _suppressDeactivateHide;
    private string _lastMainPrompt = "";
    private string _lastFooter = "Calculator";
    private CalcResult? _lastResult;
    private DispatcherTimer? _busyDelay;
    private DispatcherTimer? _shimmerTimer;
    private double _shimmerPhase;
    private int _busyDelayGeneration;
    private SettingsWindow? _settingsWindow;

    public MainWindow(CalculatorService calculator)
    {
        _calculator = calculator;
        InitializeComponent();

        SourceInitialized += (_, _) =>
        {
            ApplyWin11Chrome();
            PositionCentered();
        };

        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(SettingsStore.Current.AutoSendDelayMs) };
        _debounce.Tick += async (_, _) =>
        {
            _debounce.Stop();
            if (SettingsStore.Current.AutoSend)
                await EvaluateMainAsync();
        };

        SettingsStore.Changed += ApplySettings;
        ApplySettings();
        HideOverlay();
    }

    public void OpenSettings()
    {
        // Real settings app window (PowerToys-style), not a tiny dialog
        if (_settingsWindow is not null)
        {
            if (_settingsWindow.WindowState == WindowState.Minimized)
                _settingsWindow.WindowState = WindowState.Maximized;
            _settingsWindow.Activate();
            return;
        }

        _debounce.Stop();
        _settingsWindow = new SettingsWindow();
        _settingsWindow.Closed += (_, _) =>
        {
            _settingsWindow = null;
            ApplySettings();
        };
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void Settings_Click(object sender, MouseButtonEventArgs e)
    {
        OpenSettings();
        e.Handled = true;
    }

    private void ApplySettings()
    {
        var s = SettingsStore.Current;
        _debounce.Interval = TimeSpan.FromMilliseconds(s.AutoSendDelayMs);
        Topmost = s.StayOnTop;
        SendModeLabel.Text = s.AutoSend ? $"Auto {s.AutoSendDelayMs}ms" : "Enter";

        if (!s.EnableChatHistory)
        {
            _history.Clear();
            FollowUpPanel.Visibility = Visibility.Collapsed;
            FollowUpBox.Text = "";
        }
        else
        {
            while (_history.Count > s.HistoryMessageCount)
                _history.RemoveAt(0);
        }
    }

    private void ApplyWin11Chrome()
    {
        var hwnd = new WindowInteropHelper(this).Handle;

        // Immersive dark mode
        var dark = 1;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));

        // Acrylic (transient) - Fluent flyout / PowerToys Run / Win+S feel
        var acrylic = 3; // DWMSBT_TRANSIENTWINDOW
        if (DwmSetWindowAttribute(hwnd, 38, ref acrylic, sizeof(int)) != 0)
        {
            // Fallback to Mica Alt if acrylic unsupported
            var mica = 4; // DWMSBT_TABBEDWINDOW
            if (DwmSetWindowAttribute(hwnd, 38, ref mica, sizeof(int)) != 0)
            {
                var micaStd = 2; // DWMSBT_MAINWINDOW
                DwmSetWindowAttribute(hwnd, 38, ref micaStd, sizeof(int));
            }
        }

        // Rounded corners (Win11)
        var corner = 2; // DWMWCP_ROUND
        DwmSetWindowAttribute(hwnd, 33, ref corner, sizeof(int));

        // Slightly thicker dark border so the flyout reads against busy wallpapers
        var border = unchecked((int)0x00202020);
        DwmSetWindowAttribute(hwnd, 34, ref border, sizeof(int)); // DWMWA_BORDER_COLOR
    }

    private void PositionCentered()
    {
        var work = SystemParameters.WorkArea;
        Left = work.Left + (work.Width - Width) / 2;
        Top = work.Top + (work.Height - Height) / 3;
    }

    public void ToggleOverlay()
    {
        if (_isVisible) HideOverlay();
        else ShowOverlay();
    }

    public void ShowOverlay()
    {
        _suppressDeactivateHide = true;
        PositionCentered();
        Show();
        Activate();
        _isVisible = true;
        EquationBox.Focus();
        if (SettingsStore.Current.SelectAllOnOpen)
            EquationBox.SelectAll();
        Dispatcher.BeginInvoke(() => _suppressDeactivateHide = false, DispatcherPriority.ApplicationIdle);
    }

    public void HideOverlay()
    {
        _debounce.Stop();
        _evalCts?.Cancel();
        Hide();
        _isVisible = false;
    }

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        if (_suppressDeactivateHide) return;
        if (!SettingsStore.Current.HideWhenUnfocused) return;
        if (_isVisible) HideOverlay();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            HideOverlay();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.C &&
            (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control &&
            (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
        {
            if (!SettingsStore.Current.CopyAnswerWithCtrlC)
                return;

            // Let the textbox keep normal copy when it has a selection
            if (EquationBox.IsKeyboardFocused && EquationBox.SelectionLength > 0)
                return;
            if (FollowUpBox.IsKeyboardFocused && FollowUpBox.SelectionLength > 0)
                return;

            if (TryCopyAnswer())
                e.Handled = true;
        }
    }

    private void EquationBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        // New top-level question = reset the thread
        if (!string.Equals(EquationBox.Text?.Trim(), _lastMainPrompt, StringComparison.Ordinal))
        {
            _history.Clear();
            FollowUpPanel.Visibility = Visibility.Collapsed;
            FollowUpBox.Text = "";
        }

        if (string.IsNullOrWhiteSpace(EquationBox.Text))
        {
            _debounce.Stop();
            ClearResult();
            SetBusy(false);
            return;
        }

        // Auto-send mode only — Enter-only mode waits for the key
        if (SettingsStore.Current.AutoSend)
        {
            _debounce.Stop();
            _debounce.Start();
        }
        else
        {
            _debounce.Stop();
        }
    }

    private void EquationBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            HideOverlay();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            _debounce.Stop();
            // Ctrl+Enter copies the current answer
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                TryCopyAnswer();
                e.Handled = true;
                return;
            }
            _ = EvaluateMainAsync();
            e.Handled = true;
        }
    }

    private void FollowUpBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            HideOverlay();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            _ = EvaluateFollowUpAsync();
            e.Handled = true;
        }
    }

    private async Task EvaluateMainAsync()
    {
        var text = EquationBox.Text?.Trim() ?? "";
        if (text.Length == 0)
        {
            ClearResult();
            SetBusy(false);
            return;
        }

        _lastMainPrompt = text;
        _history.Clear();
        FollowUpBox.Text = "";

        await RunQueryAsync(text, history: null, appendToHistory: true);
    }

    private async Task EvaluateFollowUpAsync()
    {
        if (!SettingsStore.Current.EnableChatHistory)
            return;

        var text = FollowUpBox.Text?.Trim() ?? "";
        if (text.Length == 0 || _history.Count == 0)
            return;

        FollowUpBox.Text = "";

        // "thanks" / "ok" / "cool" - don't burn API or re-teach the math
        if (GroqParser.IsAcknowledgement(text))
        {
            ShowResult(CalcResult.Explain("No problem."));
            FollowUpPanel.Visibility = Visibility.Visible;
            FollowUpBox.Focus();
            return;
        }

        await RunQueryAsync(text, history: _history.ToList(), appendToHistory: true);
        FollowUpBox.Focus();
    }

    private async Task RunQueryAsync(string text, List<ChatTurn>? history, bool appendToHistory)
    {
        _evalCts?.Cancel();
        _evalCts = new CancellationTokenSource();
        var ct = _evalCts.Token;
        var gen = Interlocked.Increment(ref _evalGeneration);
        var isFollowUp = history is { Count: > 0 };
        var preserved = isFollowUp && _lastResult is { Ok: true } ? _lastResult : null;

        // Don't flash Thinking for instant local math - only after ~180ms wait
        ScheduleBusy(gen);

        try
        {
            var result = await _calculator.EvaluateAsync(text, history, ct).ConfigureAwait(true);

            // Superseded by a newer query - that request owns busy UI
            if (gen != _evalGeneration)
                return;

            CancelBusyDelay();

            // Cancelled (Esc / hide) - clear spinner and restore prior answer if any
            if (ct.IsCancellationRequested)
            {
                FinishBusy(preserved);
                return;
            }

            SetBusy(false);

            // Follow-up fail: restore previous good answer (busy already hid the panels)
            if (isFollowUp && !result.Ok && preserved is not null)
            {
                ShowResult(preserved);
                FollowUpPanel.Visibility = Visibility.Visible;
                return;
            }

            ShowResult(result);

            var historyOn = SettingsStore.Current.EnableChatHistory;
            if (appendToHistory && result.Ok && historyOn)
            {
                _history.Add(new ChatTurn { Role = "user", Content = text });
                var assistantJson = result.RawReply is not null
                    ? GroqParser.ToAssistantJson(result.RawReply)
                    : result.Kind == CalcResultKind.Explain
                        ? GroqParser.ToAssistantJson(AiReply.Explain(result.Markdown ?? ""))
                        : GroqParser.ToAssistantJson(AiReply.Calc(result.ExpressionUsed ?? result.Display));
                _history.Add(new ChatTurn { Role = "assistant", Content = assistantJson });

                var max = SettingsStore.Current.HistoryMessageCount;
                while (_history.Count > max)
                    _history.RemoveAt(0);

                FollowUpPanel.Visibility = Visibility.Visible;
            }
            else if (appendToHistory && result.Ok)
            {
                FollowUpPanel.Visibility = Visibility.Collapsed;
            }
            else if (!result.Ok)
            {
                FollowUpPanel.Visibility = historyOn && _history.Count > 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }
        catch (OperationCanceledException)
        {
            if (gen != _evalGeneration)
                return;
            CancelBusyDelay();
            FinishBusy(preserved);
        }
        catch (Exception)
        {
            if (gen != _evalGeneration)
                return;

            CancelBusyDelay();
            SetBusy(false);
            // Follow-up outage: keep prior answer, don't flash ???
            if (isFollowUp && preserved is not null)
            {
                ShowResult(preserved);
                FollowUpPanel.Visibility = Visibility.Visible;
                FlashFooter("Retry failed");
                return;
            }

            ShowUnknown();
        }
    }

    private void FinishBusy(CalcResult? restore)
    {
        CancelBusyDelay();
        SetBusy(false);
        if (restore is not null)
            ShowResult(restore);
    }

    private void ShowResult(CalcResult result)
    {
        _lastResult = result;
        IdleHint.Visibility = Visibility.Collapsed;
        switch (result.Kind)
        {
            case CalcResultKind.Number:
                ExplainScroll.Visibility = Visibility.Collapsed;
                ExplainBlock.Inlines.Clear();
                ResultText.Text = result.Display;
                ApplyResultIcon(result.Icon);
                if (!string.IsNullOrWhiteSpace(result.SecondaryDisplay))
                {
                    SecondaryResultText.Text = result.SecondaryDisplay;
                    SecondaryResultText.Visibility = Visibility.Visible;
                }
                else
                {
                    SecondaryResultText.Text = "";
                    SecondaryResultText.Visibility = Visibility.Collapsed;
                }
                NumberPanel.Visibility = Visibility.Visible;
                SetFooter("Calculator");
                break;

            case CalcResultKind.Explain:
                NumberPanel.Visibility = Visibility.Collapsed;
                ResultText.Text = "";
                ResultIcon.Visibility = Visibility.Collapsed;
                SecondaryResultText.Text = "";
                SecondaryResultText.Visibility = Visibility.Collapsed;
                MarkdownInlines.Fill(ExplainBlock, result.Markdown ?? "");
                ExplainScroll.Visibility = Visibility.Visible;
                SetFooter("Math");
                break;

            case CalcResultKind.Unknown:
                ShowUnknown();
                break;

            default:
                ClearResult();
                break;
        }
    }

    private void ShowUnknown()
    {
        _lastResult = CalcResult.Unknown();
        IdleHint.Visibility = Visibility.Collapsed;
        ExplainScroll.Visibility = Visibility.Collapsed;
        ExplainBlock.Inlines.Clear();
        ResultText.Text = "???";
        ResultIcon.Visibility = Visibility.Collapsed;
        SecondaryResultText.Text = "";
        SecondaryResultText.Visibility = Visibility.Collapsed;
        NumberPanel.Visibility = Visibility.Visible;
        SetFooter("Calculator");
    }

    private void ClearResult()
    {
        _lastResult = null;
        NumberPanel.Visibility = Visibility.Collapsed;
        ExplainScroll.Visibility = Visibility.Collapsed;
        ExplainBlock.Inlines.Clear();
        ResultText.Text = "";
        ResultIcon.Visibility = Visibility.Collapsed;
        SecondaryResultText.Text = "";
        SecondaryResultText.Visibility = Visibility.Collapsed;
        IdleHint.Visibility = Visibility.Visible;
        SetFooter("Calculator");
        FollowUpPanel.Visibility = Visibility.Collapsed;
        FollowUpBox.Text = "";
        _history.Clear();
        _lastMainPrompt = "";
    }

    private bool TryCopyAnswer()
    {
        var text = _lastResult?.CopyText;
        if (string.IsNullOrWhiteSpace(text) || text == "???")
            return false;

        try
        {
            Clipboard.SetText(text.Trim());
            FlashFooter("Copied");
            return true;
        }
        catch
        {
            FlashFooter("Copy failed");
            return false;
        }
    }

    private static readonly Brush IconUp = FreezeBrush("#6CCB5F");
    private static readonly Brush IconDown = FreezeBrush("#E81123");

    private static Brush FreezeBrush(string hex)
    {
        var b = (SolidColorBrush)new BrushConverter().ConvertFrom(hex)!;
        b.Freeze();
        return b;
    }

    private void ApplyResultIcon(ResultIconKind kind)
    {
        var glyph = ResultPresenter.Glyph(kind);
        if (string.IsNullOrEmpty(glyph) || kind == ResultIconKind.None)
        {
            ResultIcon.Visibility = Visibility.Collapsed;
            return;
        }

        ResultIcon.Text = glyph;
        ResultIcon.Foreground = kind switch
        {
            ResultIconKind.Increase => IconUp,
            ResultIconKind.Decrease => IconDown,
            ResultIconKind.Neutral => (Brush)FindResource("TextTertiary"),
            _ => (Brush)FindResource("AccentFill")
        };
        ResultIcon.Visibility = Visibility.Visible;
    }

    private void SetFooter(string text)
    {
        _lastFooter = text;
        FooterLabel.Text = text;
    }

    private void FlashFooter(string text)
    {
        FooterLabel.Text = text;
        _footerReset?.Stop();
        _footerReset = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
        _footerReset.Tick += (_, _) =>
        {
            _footerReset.Stop();
            FooterLabel.Text = _lastFooter;
        };
        _footerReset.Start();
    }

    private void ScheduleBusy(int gen)
    {
        CancelBusyDelay();
        _busyDelayGeneration = gen;
        _busyDelay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        _busyDelay.Tick += (_, _) =>
        {
            _busyDelay.Stop();
            if (_busyDelayGeneration == _evalGeneration)
                SetBusy(true);
        };
        _busyDelay.Start();
    }

    private void CancelBusyDelay()
    {
        _busyDelay?.Stop();
        _busyDelay = null;
    }

    private void SetBusy(bool busy)
    {
        if (busy)
        {
            NumberPanel.Visibility = Visibility.Collapsed;
            ExplainScroll.Visibility = Visibility.Collapsed;
            IdleHint.Visibility = Visibility.Collapsed;

            if (SettingsStore.Current.ShowThinkingAnimation)
            {
                BusyBar.Visibility = Visibility.Visible;
                BusyBar.IsIndeterminate = true;
                ThinkingLabel.Visibility = Visibility.Visible;
                StartShimmer();
            }
            else
            {
                BusyBar.Visibility = Visibility.Collapsed;
                ThinkingLabel.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            BusyBar.IsIndeterminate = false;
            BusyBar.Visibility = Visibility.Collapsed;
            StopShimmer();
            ThinkingLabel.Visibility = Visibility.Collapsed;
        }
    }

    private void StartShimmer()
    {
        _shimmerPhase = 0;
        _shimmerTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _shimmerTimer.Tick -= ShimmerTick;
        _shimmerTimer.Tick += ShimmerTick;
        ApplyShimmer(0);
        _shimmerTimer.Start();
    }

    private void StopShimmer()
    {
        _shimmerTimer?.Stop();
    }

    private void ShimmerTick(object? sender, EventArgs e)
    {
        // Full L→R cycle ~1.2s at ~60fps
        _shimmerPhase += 1.0 / 75.0;
        if (_shimmerPhase >= 1.0)
            _shimmerPhase = 0;

        ApplyShimmer(_shimmerPhase);
    }

    private void ApplyShimmer(double phase)
    {
        // Hot spot travels past both edges (no clamp - WPF allows offset outside 0..1)
        var center = phase * 1.4 - 0.2;
        ThinkEdgeL.Offset = center - 0.2;
        ThinkHot.Offset = center;
        ThinkEdgeR.Offset = center + 0.2;

        // Re-assign brush so TextBlock actually repaints the sweep
        ThinkingLabel.Foreground = null;
        ThinkingLabel.Foreground = ThinkBrush;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
}
