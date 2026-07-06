using CommunityToolkit.Mvvm.ComponentModel;
using ChessGUI.Core.Board;

namespace ChessGUI.App.ViewModels;

/// <summary>Analiz panosunda gösterilen tek bir MultiPV satırı (değerlendirme + varyant).</summary>
public sealed partial class PvLine : ObservableObject
{
    [ObservableProperty] private int _rank;        // 1..MultiPV
    [ObservableProperty] private string _evalText = "";
    [ObservableProperty] private string _moves = "";
    [ObservableProperty] private int _depth;
    [ObservableProperty] private bool _hasData;
    [ObservableProperty] private int _fromSquare = Squares.None;
    [ObservableProperty] private int _toSquare = Squares.None;
}
