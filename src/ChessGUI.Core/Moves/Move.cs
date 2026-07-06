using ChessGUI.Core.Board;

namespace ChessGUI.Core.Moves;

[Flags]
public enum MoveFlags : byte
{
    None = 0,
    Capture = 1 << 0,
    DoublePawnPush = 1 << 1,
    EnPassant = 1 << 2,
    KingCastle = 1 << 3,   // kısa rok (0-0)
    QueenCastle = 1 << 4,  // uzun rok (0-0-0)
    Promotion = 1 << 5
}

/// <summary>
/// Tek bir hamle. Kaynak/hedef kare, bayraklar ve (varsa) terfi taşı türünü tutar.
/// </summary>
public readonly record struct Move(int From, int To, MoveFlags Flags, PieceType Promotion = PieceType.None)
{
    public bool IsCapture => (Flags & MoveFlags.Capture) != 0;
    public bool IsEnPassant => (Flags & MoveFlags.EnPassant) != 0;
    public bool IsDoublePawnPush => (Flags & MoveFlags.DoublePawnPush) != 0;
    public bool IsKingCastle => (Flags & MoveFlags.KingCastle) != 0;
    public bool IsQueenCastle => (Flags & MoveFlags.QueenCastle) != 0;
    public bool IsCastle => (Flags & (MoveFlags.KingCastle | MoveFlags.QueenCastle)) != 0;
    public bool IsPromotion => (Flags & MoveFlags.Promotion) != 0;

    /// <summary>UCI uzun cebirsel notasyonu, örn. "e2e4", "e7e8q".</summary>
    public string ToUci()
    {
        string s = Squares.ToAlgebraic(From) + Squares.ToAlgebraic(To);
        if (IsPromotion)
        {
            char p = Promotion switch
            {
                PieceType.Knight => 'n',
                PieceType.Bishop => 'b',
                PieceType.Rook => 'r',
                PieceType.Queen => 'q',
                _ => 'q'
            };
            s += p;
        }
        return s;
    }

    public override string ToString() => ToUci();
}
