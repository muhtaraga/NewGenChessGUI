namespace ChessGUI.Core.Board;

/// <summary>
/// Kare yardımcıları. Kareler 0..63 aralığında tam sayıdır:
/// <c>index = rank * 8 + file</c>, dolayısıyla a1 = 0, h1 = 7, a8 = 56, h8 = 63.
/// File 0 = 'a' dosyası, Rank 0 = 1. yatay.
/// </summary>
public static class Squares
{
    public const int Count = 64;
    public const int None = -1;

    public static int FileOf(int square) => square & 7;
    public static int RankOf(int square) => square >> 3;
    public static int Of(int file, int rank) => rank * 8 + file;

    public static bool IsValidFileRank(int file, int rank) =>
        file >= 0 && file < 8 && rank >= 0 && rank < 8;

    /// <summary>Cebirsel notasyon üretir, örn. 0 -> "a1", 63 -> "h8".</summary>
    public static string ToAlgebraic(int square)
    {
        if (square is < 0 or >= Count) return "-";
        char file = (char)('a' + FileOf(square));
        char rank = (char)('1' + RankOf(square));
        return $"{file}{rank}";
    }

    /// <summary>"e4" gibi cebirsel notasyonu kareye çevirir. Geçersizse <see cref="None"/>.</summary>
    public static int FromAlgebraic(ReadOnlySpan<char> text)
    {
        if (text.Length != 2) return None;
        int file = text[0] - 'a';
        int rank = text[1] - '1';
        return IsValidFileRank(file, rank) ? Of(file, rank) : None;
    }
}
