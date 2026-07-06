using ChessGUI.Core.Board;
using ChessGUI.Core.Moves;

namespace ChessGUI.App.Controls;

/// <summary>Tahtanın hangi rengin altta olduğu.</summary>
public enum BoardOrientation
{
    WhiteBottom,
    BlackBottom
}

/// <summary>
/// <see cref="BoardControl"/> ile ViewModel arasındaki sözleşme. Kontrol saf görünümdür;
/// tüm satranç mantığını (yasal hedefler, hamle yürütme) bu arayüz üzerinden ViewModel sağlar.
/// </summary>
public interface IBoardInteraction
{
    Position Position { get; }
    BoardOrientation Orientation { get; }

    /// <summary>Son oynanan hamle (vurgu için); yoksa null.</summary>
    Move? LastMove { get; }

    /// <summary>Seçili taşın gidebileceği yasal hedef kareler (vurgu için).</summary>
    IReadOnlyList<int> GetLegalTargets(int fromSquare);

    /// <summary><paramref name="to"/> karesi, <paramref name="from"/> için yasal bir hedef mi?</summary>
    bool IsLegalTarget(int from, int to);

    /// <summary>Bu hamle bir piyon terfisi mi? (Terfi seçici göstermek için.)</summary>
    bool IsPromotion(int from, int to);

    /// <summary>Hamleyi yürütür; başarılıysa true. Terfi taşı gerekiyorsa <paramref name="promotion"/> kullanılır.</summary>
    bool TryMove(int from, int to, PieceType promotion = PieceType.Queen);

    /// <summary>Görünümün yeniden çizilmesi gerektiğinde tetiklenir.</summary>
    event EventHandler? Changed;

    /// <summary>Bir hamle başarıyla oynandığında tetiklenir (taş kayma animasyonu için).</summary>
    event EventHandler<Move>? MovePlayed;
}
