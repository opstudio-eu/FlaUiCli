using System.Collections.ObjectModel;
using System.Windows;

namespace FlaUiCli.TestApp;

public partial class MainWindow : Window
{
    private int _clickCount = 0;
    
    public ObservableCollection<Person> People { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        
        // Initialize sample data
        People.Add(new Person { Name = "John Doe", Age = 30, City = "New York" });
        People.Add(new Person { Name = "Jane Smith", Age = 25, City = "Los Angeles" });
        People.Add(new Person { Name = "Bob Johnson", Age = 45, City = "Chicago" });
        People.Add(new Person { Name = "Alice Brown", Age = 35, City = "Houston" });
        People.Add(new Person { Name = "Charlie Wilson", Age = 28, City = "Phoenix" });
        
        PeopleDataGrid.ItemsSource = People;
    }

    private void SimpleButton_Click(object sender, RoutedEventArgs e)
    {
        _clickCount++;
        ClickCountText.Text = _clickCount.ToString();
        StatusText.Text = $"Button clicked at {DateTime.Now:HH:mm:ss}";
    }

    private void IncreaseProgress_Click(object sender, RoutedEventArgs e)
    {
        if (TaskProgress.Value < 100)
        {
            TaskProgress.Value += 10;
        }
        else
        {
            TaskProgress.Value = 0;
        }
    }

    private void ShowMessage_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("This is a test message box!", "Test Message", 
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ShowInputDialog_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new InputDialog();
        if (dialog.ShowDialog() == true)
        {
            StatusText.Text = $"Input received: {dialog.InputText}";
        }
    }
}

public class Person
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public string City { get; set; } = string.Empty;
}
