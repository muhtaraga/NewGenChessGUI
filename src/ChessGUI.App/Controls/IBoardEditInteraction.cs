using ChessGUI.Core.Board;

namespace ChessGUI.App.Controls;

/// <summary>
/// Pozisyon ayarlama (grafiksel tahta editörü) sırasında <see cref="BoardControl"/>'ün
/// sürdüğü minimal sözleşme. <see cref="IBoardInteraction"/>'dan kasıtlı olarak ayrıdır:
/// burada yasallık/hamle kavramı yoktur, sadece serbest taş yerleştirme/kaldırma vardır.
/// </summary>
public interface IBoardEditInteraction
{
    /// <summary>Editörün mevcut durumundan türetilen anlık pozisyon (sadece görüntüleme için).</summary>
    Position Position { get; }

    BoardOrientation Orientation { get; }

    /// <summary>Palette'ten seçili taş; null ise dolu bir kareye tıklamak o kareyi boşaltır.</summary>
    Piece? ArmedPiece { get; }

    event EventHandler? Changed;

    /// <summary>Verilen kareye taşı yerleştirir (aynı karede taş varsa üzerine yazar).</summary>
    void PlacePiece(int square, Piece piece);

    /// <summary>Verilen karedeki taşı kaldırır (boşsa hiçbir şey yapmaz).</summary>
    void ClearSquare(int square);
}
