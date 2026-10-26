using System.Windows;
using QuickCalcAI.Services;

namespace QuickCalcAI;

public partial class App : Application
{
    private MainWindow? _window;
    private HotkeyService? _hotkey;
    private TrayService? _tray;
    private GroqParser? _groq;
    private bool _exiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            if (!SecretsStore.TryLoad(out var secrets))
            {
                var setup = new SetupWindow();
                var ok = setup.ShowDialog() == true && setup.Result is not null;
                if (!ok)
                {
                    Shutdown();
                    return;
                }

                secrets = setup.Result!;
            }

            var math = new MathEngine();
            _groq = new GroqParser(secrets.GroqApiKey, secrets.GroqModel);
            var calculator = new CalculatorService(math, _groq);

            _window = new MainWindow(calculator);

            _tray = new TrayService();
            _tray.OpenRequested += () => Dispatcher.Invoke(() => _window?.ShowOverlay());
            _tray.ExitRequested += () => Dispatcher.Invoke(ExitApp);

            _hotkey = new HotkeyService();
            _hotkey.HotkeyPressed += () => Dispatcher.Invoke(() => _window?.ToggleOverlay());
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "QuickCalcAI", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void ExitApp()
    {
        if (_exiting) return;
        _exiting = true;

        _hotkey?.Dispose();
        _tray?.Dispose();
        _groq?.Dispose();
        _window?.Close();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (!_exiting)
        {
            _hotkey?.Dispose();
            _tray?.Dispose();
            _groq?.Dispose();
        }
        base.OnExit(e);
    }
}
