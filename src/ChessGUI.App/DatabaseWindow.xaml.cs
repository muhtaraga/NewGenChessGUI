using System.Windows;
using System.Windows.Input;
using ChessGUI.App.ViewModels;

namespace ChessGUI.App;

/// <summary>Oyun veritabanı penceresi: arama, içe aktarma ve seçili oyunu tahtaya yükleme.</summary>
public partial class DatabaseWindow : Window
{
    private readonly DatabaseViewModel _vm;

    public DatabaseWindow(DatabaseViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        _vm.GameOpened += OnGameOpened;
        Closed += (_, _) => _vm.GameOpened -= OnGameOpened;
    }

    private void OnGameOpened() => Close();

    private void OnGridDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_vm.OpenSelectedGameCommand.CanExecute(null))
            _vm.OpenSelectedGameCommand.Execute(null);
    }
}
