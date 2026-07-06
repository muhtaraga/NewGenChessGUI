namespace ChessGUI.Core.Board;

/// <summary>Kalan rok hakları. Bit maskesi olarak Zobrist ve FEN'de kullanılır.</summary>
[Flags]
public enum CastlingRights : byte
{
    None = 0,
    WhiteKing = 1 << 0,   // K
    WhiteQueen = 1 << 1,  // Q
    BlackKing = 1 << 2,   // k
    BlackQueen = 1 << 3,  // q
    All = WhiteKing | WhiteQueen | BlackKing | BlackQueen
}
