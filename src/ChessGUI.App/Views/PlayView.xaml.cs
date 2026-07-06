using System.Windows.Controls;

namespace ChessGUI.App.Views;

/// <summary>
/// Oyna görünümü: bağımsız Oyna tahtası + iki saat + oyuncu adları + hamle listesi + durum/sonuç
/// butonları + pozisyon editörü katmanı. DataContext, kabuğun <c>ShellViewModel</c>'idir
/// (PlayBoard/Play/PlayMoveList alt-nesnelerine erişir).
/// </summary>
public partial class PlayView : UserControl
{
    public PlayView()
    {
        InitializeComponent();
    }
}
