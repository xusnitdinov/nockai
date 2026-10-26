using System.Diagnostics;
using System.Windows;
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
}
