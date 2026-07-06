using ChessGUI.Core.Board;
using ChessGUI.Core.Moves;

namespace ChessGUI.Core.Notation;

/// <summary>
/// Standart Cebirsel Notasyon (SAN) dönüşümleri: <see cref="Move"/> ↔ metin
/// (örn. "Nf3", "exd5", "O-O", "e8=Q+", "Qh4#").
/// </summary>
public static class San
{
    /// <summary>Bir hamleyi verilen pozisyon bağlamında SAN metnine çevirir.</summary>
    public static string ToSan(Position pos, Move move)
    {
        if (move.IsKingCastle) return AppendCheck(pos, move, "O-O");
        if (move.IsQueenCastle) return AppendCheck(pos, move, "O-O-O");

        Piece piece = pos[move.From];
        string san;

        if (piece.Type == PieceType.Pawn)
        {
            san = move.IsCapture
                ? $"{(char)('a' + Squares.FileOf(move.From))}x{Squares.ToAlgebraic(move.To)}"
                : Squares.ToAlgebraic(move.To);
            if (move.IsPromotion) san += "=" + PieceLetter(move.Promotion);
        }
        else
        {
            string disambig = Disambiguation(pos, move, piece.Type);
            string capture = move.IsCapture ? "x" : "";
            san = $"{PieceLetter(piece.Type)}{disambig}{capture}{Squares.ToAlgebraic(move.To)}";
        }

        return AppendCheck(pos, move, san);
    }

    /// <summary>SAN metnini verilen pozisyonda yasal bir hamleye çözer; bulunamazsa <c>null</c>.</summary>
    public static Move? Parse(Position pos, string san)
    {
        string target = Normalize(san);
        foreach (Move m in MoveGenerator.GenerateLegal(pos))
            if (Normalize(ToSan(pos, m)) == target)
                return m;
        return null;
    }

    private static string Disambiguation(Position pos, Move move, PieceType type)
    {
        Color us = pos.SideToMove;
        bool sameFile = false, sameRank = false, ambiguous = false;

        foreach (Move other in MoveGenerator.GenerateLegal(pos))
        {
            if (other.To != move.To || other.From == move.From) continue;
            if (pos[other.From].Type != type || pos[other.From].Color != us) continue;
            ambiguous = true;
            if (Squares.FileOf(other.From) == Squares.FileOf(move.From)) sameFile = true;
            if (Squares.RankOf(other.From) == Squares.RankOf(move.From)) sameRank = true;
        }

        if (!ambiguous) return "";
        if (!sameFile) return $"{(char)('a' + Squares.FileOf(move.From))}";
        if (!sameRank) return $"{(char)('1' + Squares.RankOf(move.From))}";
        return Squares.ToAlgebraic(move.From);
    }

    private static string AppendCheck(Position pos, Move move, string san)
    {
        var undo = pos.MakeMove(move);
        bool check = pos.IsSideToMoveInCheck();
        bool anyMove = MoveGenerator.GenerateLegal(pos).Count > 0;
        pos.UnmakeMove(move, undo);

        if (check) return san + (anyMove ? "+" : "#");
        return san;
    }

    private static string PieceLetter(PieceType type) => type switch
    {
        PieceType.Knight => "N",
        PieceType.Bishop => "B",
        PieceType.Rook => "R",
        PieceType.Queen => "Q",
        PieceType.King => "K",
        _ => ""
    };

    /// <summary>Karşılaştırma için '+', '#', '!?' gibi süsleri ve 0/O farkını normalize eder.</summary>
    private static string Normalize(string san)
    {
        Span<char> buffer = stackalloc char[san.Length];
        int n = 0;
        foreach (char c in san)
        {
            if (c is '+' or '#' or '!' or '?' or ' ') continue;
            buffer[n++] = c == '0' ? 'O' : c; // 0-0 -> O-O
        }
        return new string(buffer[..n]);
    }
}
