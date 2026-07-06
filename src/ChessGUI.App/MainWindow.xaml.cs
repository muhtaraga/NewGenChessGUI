using System.Windows;
using ChessGUI.App.ViewModels;

namespace ChessGUI.App;

/// <summary>Uygulama kabuğu. DataContext, App.OnStartup içinde kurulan ShellViewModel'e atanır.</summary>
public partial class MainWindow : Window
{
    private DatabaseWindow? _databaseWindow;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnOpenDatabase(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ShellViewModel vm) return;

        // Tek örnek: zaten açıksa öne getir (veritabanı açık kalır).
        if (_databaseWindow is { IsLoaded: true })
        {
            _databaseWindow.Activate();
            return;
        }

        _databaseWindow = new DatabaseWindow(vm.Database) { Owner = this };
        _databaseWindow.Closed += (_, _) => _databaseWindow = null;
        _databaseWindow.Show();
    }

    private void OnWindowClosed(object? sender, System.EventArgs e)
    {
        _databaseWindow?.Close();
        // Motor alt-süreçlerini (analiz + oyun) düzgünce kapat.
        (DataContext as ShellViewModel)?.Dispose();
    }
}
