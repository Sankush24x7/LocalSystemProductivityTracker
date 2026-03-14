using System.Windows;
using ProductivityTracker.App.Services;

namespace ProductivityTracker.App;

public partial class PinPromptWindow : Window
{
    private readonly string _storedHash;

    public bool IsVerified { get; private set; }

    public PinPromptWindow(string storedHash, string hint)
    {
        _storedHash = storedHash;
        InitializeComponent();
        HintText.Text = hint;
        Loaded += (_, _) => PinBox.Focus();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        string pin = PinBox.Password.Trim();
        if (!PinSecurity.Verify(pin, _storedHash))
        {
            System.Windows.MessageBox.Show("Invalid PIN.", "Unlock Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            PinBox.Clear();
            PinBox.Focus();
            return;
        }

        IsVerified = true;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
