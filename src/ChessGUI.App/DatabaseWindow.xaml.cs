using System.Linq;
using System.Windows;
using System.Windows.Input;
using ChessGUI.App.ViewModels;
using ChessGUI.Data.Entities;

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

    private void OnDeleteSelectedClick(object sender, RoutedEventArgs e)
    {
        var selected = ResultsGrid.SelectedItems.Cast<GameHeader>().ToList();
        if (selected.Count == 0) return;

        string msg = selected.Count == 1
            ? $"\"{selected[0].White} – {selected[0].Black}\" oyunu silinsin mi?"
            : $"{selected.Count} oyun silinsin mi?";
        if (MessageBox.Show(msg, "Oyunları Sil", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        _vm.DeleteGames(selected);
    }
}
