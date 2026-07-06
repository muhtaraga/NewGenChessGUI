using System.Windows.Controls;

namespace ChessGUI.App.Views;

/// <summary>
/// Motor Yönetimi görünümü: sol tarafta kayıtlı motor listesi, sağda seçili motorun jenerik UCI
/// seçenek editörü (motor hangi seçeneği sunuyorsa otomatik çıkar). DataContext, kabuğun
/// <c>ShellViewModel</c>'idir (<c>EngineManager</c> alt-nesnesine erişir).
/// </summary>
public partial class EngineManagerView : UserControl
{
    public EngineManagerView()
    {
        InitializeComponent();
    }
}
