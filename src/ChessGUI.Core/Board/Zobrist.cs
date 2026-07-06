namespace ChessGUI.Core.Board;

/// <summary>
/// Zobrist hashing anahtarları. Pozisyon tekrar tespiti (3-fold), transpozisyon
/// ve veritabanı pozisyon indeksi için 64-bit hash üretmekte kullanılır.
/// Anahtarlar sabit tohumla üretilir; böylece hash değerleri çalışmalar arası tutarlıdır.
/// </summary>
public static class Zobrist
{
    // [color(2)][type(7)][square(64)] — type 0 (None) kullanılmaz ama indeks basit kalsın diye ayrılır.
    public static readonly ulong[,,] Pieces = new ulong[2, 7, 64];
    public static readonly ulong[] Castling = new ulong[16]; // rok hakları bit maskesi (bkz. CastlingRights)
    public static readonly ulong[] EnPassantFile = new ulong[8];
    public static readonly ulong SideToMove;

    static Zobrist()
    {
        var rng = new SplitMix64(0x9E3779B97F4A7C15UL);
        for (int c = 0; c < 2; c++)
            for (int t = 1; t < 7; t++)
                for (int sq = 0; sq < 64; sq++)
                    Pieces[c, t, sq] = rng.Next();

        for (int i = 0; i < Castling.Length; i++) Castling[i] = rng.Next();
        for (int i = 0; i < EnPassantFile.Length; i++) EnPassantFile[i] = rng.Next();
        SideToMove = rng.Next();
    }

    /// <summary>Deterministik 64-bit üreteç (SplitMix64).</summary>
    private struct SplitMix64(ulong seed)
    {
        private ulong _state = seed;

        public ulong Next()
        {
            _state += 0x9E3779B97F4A7C15UL;
            ulong z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
