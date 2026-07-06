using ChessGUI.Core.Board;
using ChessGUI.Core.Moves;

namespace ChessGUI.Core.Game;

public enum GameStatus
{
    Ongoing,
    Checkmate,
    Stalemate,
    FiftyMoveRule,
    InsufficientMaterial
}

/// <summary>Bir pozisyonun sona erip ermediğini (mat, pat, beraberlik) belirler.</summary>
public static class GameEnd
{
    public static GameStatus Evaluate(Position pos)
    {
        bool hasMove = MoveGenerator.GenerateLegal(pos).Count > 0;
        if (!hasMove)
            return pos.IsSideToMoveInCheck() ? GameStatus.Checkmate : GameStatus.Stalemate;

        if (pos.HalfmoveClock >= 100) return GameStatus.FiftyMoveRule;
        if (IsInsufficientMaterial(pos)) return GameStatus.InsufficientMaterial;
        return GameStatus.Ongoing;
    }

    /// <summary>Mat imkânsız mı? (K vs K, K+B vs K, K+N vs K, K+B vs K+B aynı renk kare).</summary>
    private static bool IsInsufficientMaterial(Position pos)
    {
        int bishopsLightWhite = 0, bishopsDarkWhite = 0, bishopsLightBlack = 0, bishopsDarkBlack = 0;
        int knightsWhite = 0, knightsBlack = 0;

        for (int sq = 0; sq < 64; sq++)
        {
            Piece p = pos[sq];
            if (p.IsNone) continue;
            switch (p.Type)
            {
                case PieceType.King:
                    break;
                case PieceType.Knight:
                    if (p.Color == Color.White) knightsWhite++; else knightsBlack++;
                    break;
                case PieceType.Bishop:
                    bool light = (Squares.FileOf(sq) + Squares.RankOf(sq)) % 2 != 0;
                    if (p.Color == Color.White) { if (light) bishopsLightWhite++; else bishopsDarkWhite++; }
                    else { if (light) bishopsLightBlack++; else bishopsDarkBlack++; }
                    break;
                default:
                    return false; // piyon, kale ya da vezir varsa mat mümkün
            }
        }

        int whiteMinors = bishopsLightWhite + bishopsDarkWhite + knightsWhite;
        int blackMinors = bishopsLightBlack + bishopsDarkBlack + knightsBlack;

        // Sadece şahlar.
        if (whiteMinors == 0 && blackMinors == 0) return true;
        // Bir taraf tek hafif taş.
        if (whiteMinors <= 1 && blackMinors == 0) return true;
        if (blackMinors <= 1 && whiteMinors == 0) return true;
        return false;
    }
}
