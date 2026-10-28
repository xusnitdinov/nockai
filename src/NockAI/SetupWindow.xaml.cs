using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Navigation;
using NockAI.Services;

namespace NockAI;

public partial class SetupWindow : Window
{
    public AppSecrets? Result { get; private set; }

    public SetupWindow()
    {
        InitializeComponent();
        KeyBox.Focus();
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var dark = 1;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));

        var acrylic = 3;
        if (DwmSetWindowAttribute(hwnd, 38, ref acrylic, sizeof(int)) != 0)
        {
            var mica = 2;
            DwmSetWindowAttribute(hwnd, 38, ref mica, sizeof(int));
        }

        var corner = 2;
        DwmSetWindowAttribute(hwnd, 33, ref corner, sizeof(int));
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var key = KeyBox.Text?.Trim() ?? "";
        if (key.Length < 10 || !key.StartsWith("gsk_", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(
                "That doesn't look like a Groq key (usually starts with gsk_).",
                "NockAI",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var secrets = new AppSecrets
        {
            GroqApiKey = key,
            GroqModel = "qwen/qwen3.8-27b"
        };

        try
        {
            SecretsStore.Save(secrets, preferAppData: true);
            Result = secrets;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "NockAI", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Quit_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Link_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
}
