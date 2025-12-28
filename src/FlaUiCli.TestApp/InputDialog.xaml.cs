using System.Windows;

namespace FlaUiCli.TestApp;

public partial class InputDialog : Window
{
    public string InputText => InputTextBox.Text;

    public InputDialog()
    {
        InitializeComponent();
        InputTextBox.Focus();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
