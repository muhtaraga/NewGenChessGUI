using System.Windows.Controls;

namespace ChessGUI.App.Views;

/// <summary>
/// Analiz görünümü: değerlendirme çubuğu + tahta + oyun raporu şeridi + sağ panel
/// (açılış kitabı, hamle listesi, motor analizi, FEN). DataContext, kabuğun <c>ShellViewModel</c>'idir.
/// </summary>
public partial class AnalysisView : UserControl
{
    public AnalysisView()
    {
        InitializeComponent();
    }
}
