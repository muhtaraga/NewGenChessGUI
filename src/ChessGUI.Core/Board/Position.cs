using System.Text;
using ChessGUI.Core.Moves;

namespace ChessGUI.Core.Board;

/// <summary>
/// Tam bir satranç pozisyonu: tahta, sıra, rok hakları, geçerken alma karesi,
/// yarım/tam hamle sayaçları ve artımlı Zobrist anahtarı.
/// Hamleler <see cref="MakeMove"/> / <see cref="UnmakeMove"/> ile uygulanıp geri alınır.
/// </summary>
public sealed class Position
{
    public const string StartFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    private readonly Piece[] _squares = new Piece[64];
    private readonly int[] _kingSquare = new int[2];

    private Color _sideToMove;
    private CastlingRights _castling;
    private int _enPassant;      // Squares.None ya da hedef kare
    private int _halfmoveClock;
    private int _fullmoveNumber;
    private ulong _key;

    public Color SideToMove { get => _sideToMove; private set => _sideToMove = value; }
    public CastlingRights Castling => _castling;
    public int EnPassantSquare => _enPassant;
    public int HalfmoveClock => _halfmoveClock;
    public int FullmoveNumber => _fullmoveNumber;
    public ulong ZobristKey => _key;

    public Piece this[int square] => _squares[square];
    public int KingSquare(Color color) => _kingSquare[(int)color];

    private Position() { }

    /// <summary>Başlangıç pozisyonu.</summary>
    public static Position CreateStandard() => FromFen(StartFen);

    public Position Clone()
    {
        var p = new Position();
        Array.Copy(_squares, p._squares, 64);
        Array.Copy(_kingSquare, p._kingSquare, 2);
        p._sideToMove = _sideToMove;
        p._castling = _castling;
        p._enPassant = _enPassant;
        p._halfmoveClock = _halfmoveClock;
        p._fullmoveNumber = _fullmoveNumber;
        p._key = _key;
        return p;
    }

    // --- Undo ---------------------------------------------------------------

    /// <summary>Bir hamleyi geri almak için gereken önceki durum.</summary>
    public struct Undo
    {
        public Piece Captured;
        public int CapturedSquare;
        public CastlingRights PrevCastling;
        public int PrevEnPassant;
        public int PrevHalfmove;
        public ulong PrevKey;
    }

    // --- Make / Unmake ------------------------------------------------------

    public Undo MakeMove(Move move)
    {
        Color us = _sideToMove;
        Color them = us.Opponent();
        int from = move.From, to = move.To;
        Piece moving = _squares[from];

        var undo = new Undo
        {
            Captured = Piece.None,
            CapturedSquare = -1,
            PrevCastling = _castling,
            PrevEnPassant = _enPassant,
            PrevHalfmove = _halfmoveClock,
            PrevKey = _key
        };

        // Eski geçerken-alma ve rok haklarını hash'ten çıkar.
        if (_enPassant != Squares.None) _key ^= Zobrist.EnPassantFile[Squares.FileOf(_enPassant)];
        _key ^= Zobrist.Castling[(int)_castling];

        // Alınan taş.
        if (move.IsEnPassant)
        {
            int capSq = us == Color.White ? to - 8 : to + 8;
            undo.Captured = _squares[capSq];
            undo.CapturedSquare = capSq;
            _squares[capSq] = Piece.None;
            _key ^= Zobrist.Pieces[(int)them, (int)PieceType.Pawn, capSq];
        }
        else if (move.IsCapture)
        {
            undo.Captured = _squares[to];
            undo.CapturedSquare = to;
            _key ^= Zobrist.Pieces[(int)undo.Captured.Color, (int)undo.Captured.Type, to];
        }

        // Taşı taşı.
        _squares[from] = Piece.None;
        _key ^= Zobrist.Pieces[(int)us, (int)moving.Type, from];

        Piece placed = move.IsPromotion ? new Piece(us, move.Promotion) : moving;
        _squares[to] = placed;
        _key ^= Zobrist.Pieces[(int)us, (int)placed.Type, to];

        if (moving.Type == PieceType.King)
            _kingSquare[(int)us] = to;

        // Rok hamlesinde kaleyi de taşı.
        if (move.IsKingCastle)
            MoveRook(us == Color.White ? 7 : 63, us == Color.White ? 5 : 61, us);
        else if (move.IsQueenCastle)
            MoveRook(us == Color.White ? 0 : 56, us == Color.White ? 3 : 59, us);

        // Rok haklarını güncelle.
        _castling = UpdateCastling(_castling, from, to, moving);

        // Geçerken-alma karesi.
        _enPassant = move.IsDoublePawnPush
            ? (us == Color.White ? from + 8 : from - 8)
            : Squares.None;

        if (_enPassant != Squares.None) _key ^= Zobrist.EnPassantFile[Squares.FileOf(_enPassant)];
        _key ^= Zobrist.Castling[(int)_castling];

        // 50 hamle sayacı.
        _halfmoveClock = (moving.Type == PieceType.Pawn || move.IsCapture) ? 0 : _halfmoveClock + 1;

        if (us == Color.Black) _fullmoveNumber++;
        _sideToMove = them;
        _key ^= Zobrist.SideToMove;

        return undo;
    }

    public void UnmakeMove(Move move, Undo undo)
    {
        Color us = _sideToMove.Opponent(); // hamleyi yapan taraf
        int from = move.From, to = move.To;

        _sideToMove = us;
        if (us == Color.Black) _fullmoveNumber--;

        Piece movedNow = _squares[to];
        Piece original = move.IsPromotion ? new Piece(us, PieceType.Pawn) : movedNow;
        _squares[from] = original;
        _squares[to] = Piece.None;

        if (original.Type == PieceType.King)
            _kingSquare[(int)us] = from;

        // Rok hamlesini geri al.
        if (move.IsKingCastle)
            MoveRookRaw(us == Color.White ? 5 : 61, us == Color.White ? 7 : 63);
        else if (move.IsQueenCastle)
            MoveRookRaw(us == Color.White ? 3 : 59, us == Color.White ? 0 : 56);

        // Alınan taşı geri koy.
        if (undo.Captured.Type != PieceType.None)
            _squares[undo.CapturedSquare] = undo.Captured;

        _castling = undo.PrevCastling;
        _enPassant = undo.PrevEnPassant;
        _halfmoveClock = undo.PrevHalfmove;
        _key = undo.PrevKey;
    }

    private void MoveRook(int rookFrom, int rookTo, Color us)
    {
        _squares[rookTo] = _squares[rookFrom];
        _squares[rookFrom] = Piece.None;
        _key ^= Zobrist.Pieces[(int)us, (int)PieceType.Rook, rookFrom];
        _key ^= Zobrist.Pieces[(int)us, (int)PieceType.Rook, rookTo];
    }

    private void MoveRookRaw(int fromSq, int toSq)
    {
        _squares[toSq] = _squares[fromSq];
        _squares[fromSq] = Piece.None;
    }

    private static CastlingRights UpdateCastling(CastlingRights rights, int from, int to, Piece moving)
    {
        if (moving.Type == PieceType.King)
            rights &= moving.Color == Color.White
                ? ~(CastlingRights.WhiteKing | CastlingRights.WhiteQueen)
                : ~(CastlingRights.BlackKing | CastlingRights.BlackQueen);

        // Kalenin evinden ayrılması ya da o karede alınması hakkı düşürür.
        rights = RemoveRightForSquare(rights, from);
        rights = RemoveRightForSquare(rights, to);
        return rights;
    }

    private static CastlingRights RemoveRightForSquare(CastlingRights rights, int square) => square switch
    {
        0 => rights & ~CastlingRights.WhiteQueen,   // a1
        7 => rights & ~CastlingRights.WhiteKing,    // h1
        56 => rights & ~CastlingRights.BlackQueen,  // a8
        63 => rights & ~CastlingRights.BlackKing,   // h8
        _ => rights
    };

    // --- Saldırı / şah tespiti ---------------------------------------------

    private static readonly int[] KnightDF = { 1, 2, 2, 1, -1, -2, -2, -1 };
    private static readonly int[] KnightDR = { 2, 1, -1, -2, -2, -1, 1, 2 };
    private static readonly int[] KingDF = { 1, 1, 0, -1, -1, -1, 0, 1 };
    private static readonly int[] KingDR = { 0, 1, 1, 1, 0, -1, -1, -1 };
    private static readonly int[] BishopDF = { 1, 1, -1, -1 };
    private static readonly int[] BishopDR = { 1, -1, 1, -1 };
    private static readonly int[] RookDF = { 1, -1, 0, 0 };
    private static readonly int[] RookDR = { 0, 0, 1, -1 };

    /// <summary><paramref name="square"/> karesi <paramref name="by"/> rengince tehdit ediliyor mu?</summary>
    public bool IsSquareAttacked(int square, Color by)
    {
        int f = Squares.FileOf(square), r = Squares.RankOf(square);

        // Piyon (saldıran piyon, hedefin bir yatay gerisinde/diagonalinde).
        int pawnRank = by == Color.White ? r - 1 : r + 1;
        if (pawnRank is >= 0 and < 8)
        {
            foreach (int df in stackalloc[] { -1, 1 })
            {
                int pf = f + df;
                if (pf is < 0 or > 7) continue;
                Piece p = _squares[Squares.Of(pf, pawnRank)];
                if (p.Type == PieceType.Pawn && p.Color == by) return true;
            }
        }

        // At.
        for (int i = 0; i < 8; i++)
        {
            int nf = f + KnightDF[i], nr = r + KnightDR[i];
            if (!Squares.IsValidFileRank(nf, nr)) continue;
            Piece p = _squares[Squares.Of(nf, nr)];
            if (p.Type == PieceType.Knight && p.Color == by) return true;
        }

        // Şah.
        for (int i = 0; i < 8; i++)
        {
            int nf = f + KingDF[i], nr = r + KingDR[i];
            if (!Squares.IsValidFileRank(nf, nr)) continue;
            Piece p = _squares[Squares.Of(nf, nr)];
            if (p.Type == PieceType.King && p.Color == by) return true;
        }

        // Fil / vezir (çapraz).
        if (SliderAttacks(f, r, BishopDF, BishopDR, PieceType.Bishop, by)) return true;
        // Kale / vezir (düz).
        if (SliderAttacks(f, r, RookDF, RookDR, PieceType.Rook, by)) return true;

        return false;
    }

    private bool SliderAttacks(int f, int r, int[] dfs, int[] drs, PieceType straightType, Color by)
    {
        for (int i = 0; i < 4; i++)
        {
            int nf = f + dfs[i], nr = r + drs[i];
            while (Squares.IsValidFileRank(nf, nr))
            {
                Piece p = _squares[Squares.Of(nf, nr)];
                if (p.Type != PieceType.None)
                {
                    if (p.Color == by && (p.Type == straightType || p.Type == PieceType.Queen))
                        return true;
                    break;
                }
                nf += dfs[i];
                nr += drs[i];
            }
        }
        return false;
    }

    public bool IsInCheck(Color color) => IsSquareAttacked(_kingSquare[(int)color], color.Opponent());
    public bool IsSideToMoveInCheck() => IsInCheck(_sideToMove);

    // --- FEN ----------------------------------------------------------------

    public static Position FromFen(string fen)
    {
        var p = new Position();
        string[] parts = fen.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4) throw new FormatException($"Geçersiz FEN: '{fen}'");

        // 1) Tahta — FEN 8. yataydan (siyah) başlar. Her satırın tam 8 kareye denk gelmesi
        // ve toplam 8 satır olması doğrulanır; aksi halde tahta sessizce kayar ya da dizinin
        // dışına taşar (bkz. testler).
        int rank = 7, file = 0;
        foreach (char c in parts[0])
        {
            if (c == '/')
            {
                if (file != 8 || rank == 0) throw new FormatException($"Geçersiz FEN: '{fen}'");
                rank--;
                file = 0;
            }
            else if (char.IsDigit(c))
            {
                file += c - '0';
                if (file > 8) throw new FormatException($"Geçersiz FEN: '{fen}'");
            }
            else
            {
                if (file >= 8) throw new FormatException($"Geçersiz FEN: '{fen}'");
                Piece piece = Piece.FromFenChar(c);
                if (piece.IsNone) throw new FormatException($"Geçersiz FEN taşı: '{c}'");
                int sq = Squares.Of(file, rank);
                p._squares[sq] = piece;
                if (piece.Type == PieceType.King) p._kingSquare[(int)piece.Color] = sq;
                file++;
            }
        }
        if (file != 8 || rank != 0) throw new FormatException($"Geçersiz FEN: '{fen}'");

        // 2) Sıra.
        p._sideToMove = parts[1] == "b" ? Color.Black : Color.White;

        // 3) Rok hakları.
        p._castling = CastlingRights.None;
        if (parts[2] != "-")
            foreach (char c in parts[2])
                p._castling |= c switch
                {
                    'K' => CastlingRights.WhiteKing,
                    'Q' => CastlingRights.WhiteQueen,
                    'k' => CastlingRights.BlackKing,
                    'q' => CastlingRights.BlackQueen,
                    _ => CastlingRights.None
                };

        // 4) Geçerken-alma.
        p._enPassant = parts[3] == "-" ? Squares.None : Squares.FromAlgebraic(parts[3]);

        // 5-6) Sayaçlar (opsiyonel).
        p._halfmoveClock = parts.Length > 4 && int.TryParse(parts[4], out int hm) ? hm : 0;
        p._fullmoveNumber = parts.Length > 5 && int.TryParse(parts[5], out int fm) ? fm : 1;

        p._key = p.ComputeKey();
        return p;
    }

    public string ToFen()
    {
        var sb = new StringBuilder();
        for (int rank = 7; rank >= 0; rank--)
        {
            int empty = 0;
            for (int file = 0; file < 8; file++)
            {
                Piece piece = _squares[Squares.Of(file, rank)];
                if (piece.IsNone) { empty++; continue; }
                if (empty > 0) { sb.Append(empty); empty = 0; }
                sb.Append(piece.ToFenChar());
            }
            if (empty > 0) sb.Append(empty);
            if (rank > 0) sb.Append('/');
        }

        sb.Append(_sideToMove == Color.White ? " w " : " b ");

        if (_castling == CastlingRights.None) sb.Append('-');
        else
        {
            if ((_castling & CastlingRights.WhiteKing) != 0) sb.Append('K');
            if ((_castling & CastlingRights.WhiteQueen) != 0) sb.Append('Q');
            if ((_castling & CastlingRights.BlackKing) != 0) sb.Append('k');
            if ((_castling & CastlingRights.BlackQueen) != 0) sb.Append('q');
        }

        sb.Append(' ');
        sb.Append(_enPassant == Squares.None ? "-" : Squares.ToAlgebraic(_enPassant));
        sb.Append(' ').Append(_halfmoveClock);
        sb.Append(' ').Append(_fullmoveNumber);
        return sb.ToString();
    }

    /// <summary>Zobrist anahtarını sıfırdan hesaplar (FEN yüklemesinde ve testlerde doğrulama için).</summary>
    public ulong ComputeKey()
    {
        ulong k = 0;
        for (int sq = 0; sq < 64; sq++)
        {
            Piece piece = _squares[sq];
            if (!piece.IsNone)
                k ^= Zobrist.Pieces[(int)piece.Color, (int)piece.Type, sq];
        }
        k ^= Zobrist.Castling[(int)_castling];
        if (_enPassant != Squares.None) k ^= Zobrist.EnPassantFile[Squares.FileOf(_enPassant)];
        if (_sideToMove == Color.Black) k ^= Zobrist.SideToMove;
        return k;
    }

    public override string ToString() => ToFen();
}
