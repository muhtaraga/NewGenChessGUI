using System.Windows.Controls;

namespace ChessGUI.App.Views;

/// <summary>
/// Oyna sekmesindeki grafiksel pozisyon ayarlama (editör) görünümü: taş paleti, tahta,
/// sıra/rok/en-passant kontrolleri ve Uygula/İptal butonları. DataContext, kabuğun
/// <c>ShellViewModel</c>'idir (<c>Play.Editor</c> üzerinden erişir).
/// </summary>
public partial class PositionEditorView : UserControl
{
    public PositionEditorView()
    {
        InitializeComponent();
    }
}
