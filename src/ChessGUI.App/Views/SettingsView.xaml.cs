using System.Windows.Controls;

namespace ChessGUI.App.Views;

/// <summary>
/// Ayarlar görünümü: tahta renkleri, taş boyutu/koordinatlar, ses/animasyon, varsayılan süre
/// kontrolü ve davranış (oto-çevir, oyun bitince oto-analiz). DataContext, kabuğun
/// <c>ShellViewModel</c>'idir (<c>SettingsVm</c> alt-nesnesine erişir).
/// </summary>
public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }
}
