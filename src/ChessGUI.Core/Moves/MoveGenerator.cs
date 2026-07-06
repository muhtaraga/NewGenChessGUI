using ChessGUI.Core.Board;

namespace ChessGUI.Core.Moves;

/// <summary>
/// Bir pozisyondan yasal hamleleri üretir. Önce pseudo-legal hamleler üretilir,
/// ardından her biri geçici olarak oynanıp kendi şahının tehdit altında kalmadığı
/// doğrulanarak süzülür. Bu yaklaşım (pin, açığa çıkan şah vb.) tüm durumları
/// kesin biçimde doğru ele alır.
/// </summary>
public static class MoveGenerator
{
    private static readonly PieceType[] PromotionTypes =
        { PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight };

    private static readonly int[] KnightDF = { 1, 2, 2, 1, -1, -2, -2, -1 };
    private static readonly int[] KnightDR = { 2, 1, -1, -2, -2, -1, 1, 2 };
    private static readonly int[] KingDF = { 1, 1, 0, -1, -1, -1, 0, 1 };
    private static readonly int[] KingDR = { 0, 1, 1, 1, 0, -1, -1, -1 };
    private static readonly int[] BishopDF = { 1, 1, -1, -1 };
    private static readonly int[] BishopDR = { 1, -1, 1, -1 };
    private static readonly int[] RookDF = { 1, -1, 0, 0 };
    private static readonly int[] RookDR = { 0, 0, 1, -1 };

    public static List<Move> GenerateLegal(Position pos)
    {
        var pseudo = GeneratePseudoLegal(pos);
        var legal = new List<Move>(pseudo.Count);
        Color us = pos.SideToMove;
        foreach (Move m in pseudo)
        {
            var undo = pos.MakeMove(m);
            if (!pos.IsInCheck(us))
                legal.Add(m);
            pos.UnmakeMove(m, undo);
        }
        return legal;
    }

    public static List<Move> GeneratePseudoLegal(Position pos)
    {
        var moves = new List<Move>(48);
        Color us = pos.SideToMove;

        for (int sq = 0; sq < 64; sq++)
        {
            Piece p = pos[sq];
            if (p.IsNone || p.Color != us) continue;

            switch (p.Type)
            {
                case PieceType.Pawn: GeneratePawn(pos, sq, us, moves); break;
                case PieceType.Knight: GenerateStep(pos, sq, us, KnightDF, KnightDR, moves); break;
                case PieceType.Bishop: GenerateSlider(pos, sq, us, BishopDF, BishopDR, moves); break;
                case PieceType.Rook: GenerateSlider(pos, sq, us, RookDF, RookDR, moves); break;
                case PieceType.Queen:
                    GenerateSlider(pos, sq, us, BishopDF, BishopDR, moves);
                    GenerateSlider(pos, sq, us, RookDF, RookDR, moves);
                    break;
                case PieceType.King:
                    GenerateStep(pos, sq, us, KingDF, KingDR, moves);
                    GenerateCastling(pos, us, moves);
                    break;
            }
        }
        return moves;
    }

    private static void GeneratePawn(Position pos, int from, Color us, List<Move> moves)
    {
        int f = Squares.FileOf(from), r = Squares.RankOf(from);
        int dir = us == Color.White ? 1 : -1;
        int startRank = us == Color.White ? 1 : 6;
        int promoRank = us == Color.White ? 7 : 0;

        // Tek ileri.
        int oneRank = r + dir;
        if (oneRank is >= 0 and < 8)
        {
            int one = Squares.Of(f, oneRank);
            if (pos[one].IsNone)
            {
                AddPawnMove(from, one, oneRank == promoRank, MoveFlags.None, moves);

                // Çift ileri.
                if (r == startRank)
                {
                    int two = Squares.Of(f, r + 2 * dir);
                    if (pos[two].IsNone)
                        moves.Add(new Move(from, two, MoveFlags.DoublePawnPush));
                }
            }
        }

        // Çapraz almalar + geçerken alma.
        foreach (int df in stackalloc[] { -1, 1 })
        {
            int cf = f + df;
            if (cf is < 0 or > 7 || oneRank is < 0 or >= 8) continue;
            int target = Squares.Of(cf, oneRank);

            if (target == pos.EnPassantSquare && pos.EnPassantSquare != Squares.None)
            {
                moves.Add(new Move(from, target, MoveFlags.Capture | MoveFlags.EnPassant));
                continue;
            }

            Piece t = pos[target];
            if (!t.IsNone && t.Color != us)
                AddPawnMove(from, target, oneRank == promoRank, MoveFlags.Capture, moves);
        }
    }

    private static void AddPawnMove(int from, int to, bool promotion, MoveFlags flags, List<Move> moves)
    {
        if (promotion)
            foreach (PieceType pt in PromotionTypes)
                moves.Add(new Move(from, to, flags | MoveFlags.Promotion, pt));
        else
            moves.Add(new Move(from, to, flags));
    }

    private static void GenerateStep(Position pos, int from, Color us, int[] dfs, int[] drs, List<Move> moves)
    {
        int f = Squares.FileOf(from), r = Squares.RankOf(from);
        for (int i = 0; i < dfs.Length; i++)
        {
            int nf = f + dfs[i], nr = r + drs[i];
            if (!Squares.IsValidFileRank(nf, nr)) continue;
            int to = Squares.Of(nf, nr);
            Piece t = pos[to];
            if (t.IsNone)
                moves.Add(new Move(from, to, MoveFlags.None));
            else if (t.Color != us)
                moves.Add(new Move(from, to, MoveFlags.Capture));
        }
    }

    private static void GenerateSlider(Position pos, int from, Color us, int[] dfs, int[] drs, List<Move> moves)
    {
        int f = Squares.FileOf(from), r = Squares.RankOf(from);
        for (int i = 0; i < dfs.Length; i++)
        {
            int nf = f + dfs[i], nr = r + drs[i];
            while (Squares.IsValidFileRank(nf, nr))
            {
                int to = Squares.Of(nf, nr);
                Piece t = pos[to];
                if (t.IsNone)
                    moves.Add(new Move(from, to, MoveFlags.None));
                else
                {
                    if (t.Color != us) moves.Add(new Move(from, to, MoveFlags.Capture));
                    break;
                }
                nf += dfs[i];
                nr += drs[i];
            }
        }
    }

    private static void GenerateCastling(Position pos, Color us, List<Move> moves)
    {
        Color them = us.Opponent();
        int kingSq = pos.KingSquare(us);

        // Şah altındayken rok yapılamaz.
        if (pos.IsSquareAttacked(kingSq, them)) return;

        if (us == Color.White)
        {
            // Kısa: e1->g1, f1/g1 boş, e1/f1/g1 tehdit altında değil.
            if ((pos.Castling & CastlingRights.WhiteKing) != 0 &&
                pos[5].IsNone && pos[6].IsNone &&
                !pos.IsSquareAttacked(5, them) && !pos.IsSquareAttacked(6, them))
                moves.Add(new Move(4, 6, MoveFlags.KingCastle));

            // Uzun: e1->c1, b1/c1/d1 boş, e1/d1/c1 tehdit altında değil.
            if ((pos.Castling & CastlingRights.WhiteQueen) != 0 &&
                pos[1].IsNone && pos[2].IsNone && pos[3].IsNone &&
                !pos.IsSquareAttacked(3, them) && !pos.IsSquareAttacked(2, them))
                moves.Add(new Move(4, 2, MoveFlags.QueenCastle));
        }
        else
        {
            // Kısa: e8->g8.
            if ((pos.Castling & CastlingRights.BlackKing) != 0 &&
                pos[61].IsNone && pos[62].IsNone &&
                !pos.IsSquareAttacked(61, them) && !pos.IsSquareAttacked(62, them))
                moves.Add(new Move(60, 62, MoveFlags.KingCastle));

            // Uzun: e8->c8.
            if ((pos.Castling & CastlingRights.BlackQueen) != 0 &&
                pos[57].IsNone && pos[58].IsNone && pos[59].IsNone &&
                !pos.IsSquareAttacked(59, them) && !pos.IsSquareAttacked(58, them))
                moves.Add(new Move(60, 58, MoveFlags.QueenCastle));
        }
    }
}
