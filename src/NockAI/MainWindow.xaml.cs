using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using NockAI.Services;
using WpfAnimatedGif;

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

    public MainWindow(CalculatorService calculator)
    {
        _calculator = calculator;
        InitializeComponent();

        SourceInitialized += (_, _) =>
        {
            ApplyWin11Chrome();
            PositionCentered();
        };

        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
        _debounce.Tick += async (_, _) =>
        {
            _debounce.Stop();
            await EvaluateMainAsync();
        };

        HideOverlay();
    }

    private void ApplyWin11Chrome()
    {
        var hwnd = new WindowInteropHelper(this).Handle;

        // Immersive dark mode
        var dark = 1;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));

        // Acrylic (transient) - Fluent flyout / PowerToys Run feel
        var acrylic = 3; // DWMSBT_TRANSIENTWINDOW
        if (DwmSetWindowAttribute(hwnd, 38, ref acrylic, sizeof(int)) != 0)
        {
            // Fallback to Mica if acrylic unsupported
            var mica = 2;
            DwmSetWindowAttribute(hwnd, 38, ref mica, sizeof(int));
        }

        // Rounded corners (Win11)
        var corner = 2; // DWMWCP_ROUND
        DwmSetWindowAttribute(hwnd, 33, ref corner, sizeof(int));
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

        _debounce.Stop();
        _debounce.Start();

        if (string.IsNullOrWhiteSpace(EquationBox.Text))
        {
            ClearResult();
            SetBusy(false);
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
        var text = FollowUpBox.Text?.Trim() ?? "";
        if (text.Length == 0 || _history.Count == 0)
            return;

        FollowUpBox.Text = "";
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

        SetBusy(true);

        try
        {
            var result = await _calculator.EvaluateAsync(text, history, ct).ConfigureAwait(true);

            // Superseded by a newer query - that request owns busy UI
            if (gen != _evalGeneration)
                return;

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

            if (appendToHistory && result.Ok)
            {
                _history.Add(new ChatTurn { Role = "user", Content = text });
                var assistantJson = result.RawReply is not null
                    ? GroqParser.ToAssistantJson(result.RawReply)
                    : result.Kind == CalcResultKind.Explain
                        ? GroqParser.ToAssistantJson(AiReply.Explain(result.Markdown ?? ""))
                        : GroqParser.ToAssistantJson(AiReply.Calc(result.ExpressionUsed ?? result.Display));
                _history.Add(new ChatTurn { Role = "assistant", Content = assistantJson });

                while (_history.Count > 8)
                    _history.RemoveAt(0);

                FollowUpPanel.Visibility = Visibility.Visible;
            }
            else if (!result.Ok)
            {
                FollowUpPanel.Visibility = _history.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        catch (OperationCanceledException)
        {
            if (gen != _evalGeneration)
                return;
            FinishBusy(preserved);
        }
        catch (Exception)
        {
            if (gen != _evalGeneration)
                return;

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

    private void SetBusy(bool busy)
    {
        if (busy)
        {
            NumberPanel.Visibility = Visibility.Collapsed;
            ExplainScroll.Visibility = Visibility.Collapsed;
            IdleHint.Visibility = Visibility.Collapsed;
            BusySpinner.Visibility = Visibility.Visible;
            var ctrl = ImageBehavior.GetAnimationController(BusySpinner);
            ctrl?.GotoFrame(0);
            ctrl?.Play();
        }
        else
        {
            ImageBehavior.GetAnimationController(BusySpinner)?.Pause();
            BusySpinner.Visibility = Visibility.Collapsed;
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
}
