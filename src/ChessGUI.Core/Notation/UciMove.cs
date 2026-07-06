using ChessGUI.Core.Board;
using ChessGUI.Core.Moves;

namespace ChessGUI.Core.Notation;

/// <summary>UCI uzun cebirsel notasyonu (örn. "e2e4", "e7e8q") ile <see cref="Move"/> arası dönüşüm.</summary>
public static class UciMove
{
    /// <summary>UCI hamle metnini verilen pozisyonda yasal bir hamleye çözer; bulunamazsa <c>null</c>.</summary>
    public static Move? Parse(Position pos, string uci)
    {
        if (uci.Length is < 4 or > 5) return null;
        int from = Squares.FromAlgebraic(uci.AsSpan(0, 2));
        int to = Squares.FromAlgebraic(uci.AsSpan(2, 2));
        if (from == Squares.None || to == Squares.None) return null;

        PieceType promo = uci.Length == 5 ? PromoFromChar(uci[4]) : PieceType.None;

        foreach (Move m in MoveGenerator.GenerateLegal(pos))
        {
            if (m.From != from || m.To != to) continue;
            if (m.IsPromotion)
            {
                if (m.Promotion == (promo == PieceType.None ? PieceType.Queen : promo)) return m;
            }
            else
            {
                return m;
            }
        }
        return null;
    }

    private static PieceType PromoFromChar(char c) => char.ToLowerInvariant(c) switch
    {
        'n' => PieceType.Knight,
        'b' => PieceType.Bishop,
        'r' => PieceType.Rook,
        'q' => PieceType.Queen,
        _ => PieceType.None
    };
}
