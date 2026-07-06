using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChessGUI.App.Controls;
using ChessGUI.Core.Board;

namespace ChessGUI.App.ViewModels;

/// <summary>
/// Oyna sekmesindeki grafiksel pozisyon editörünün görünüm modeli. Bir taş dizisi + sıra/rok/en
/// passant alanlarını tutar, bunlardan bir FEN üretir ve <see cref="IBoardEditInteraction"/>
/// üzerinden <see cref="Controls.BoardControl"/>'ün "düzenleme modu"nu besler.
/// </summary>
public sealed partial class PositionEditorViewModel : ObservableObject, IBoardEditInteraction
{
    private readonly Piece[] _squares = new Piece[64];

    [ObservableProperty] private Color _sideToMove = Color.White;
    [ObservableProperty] private bool _whiteKingSide = true;
    [ObservableProperty] private bool _whiteQueenSide = true;
    [ObservableProperty] private bool _blackKingSide = true;
    [ObservableProperty] private bool _blackQueenSide = true;
    [ObservableProperty] private string _enPassantSquareText = "-";
    [ObservableProperty] private Piece? _armedPiece;
    [ObservableProperty] private string _validationError = "";
    [ObservableProperty] private string _fenInput = "";

    // Not: özellik adı kasıtlı olarak "Position" DEĞİL — sınıf içinde "Position" (tür adı) ile
    // çakışmayı önlemek için arayüz üyesi açıkça (explicit) uygulanır, bkz. alt taraf.
    private Position _livePosition = Position.CreateStandard();
    Position IBoardEditInteraction.Position => _livePosition;

    public BoardOrientation Orientation { get; private set; } = BoardOrientation.WhiteBottom;

    public event EventHandler? Changed;

    public void SetOrientation(BoardOrientation orientation)
    {
        Orientation = orientation;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    partial void OnArmedPieceChanged(Piece? value) => OnPropertyChanged(nameof(ArmedPieceLabel));

    public string ArmedPieceLabel => ArmedPiece is { } p
        ? $"{(p.Color == Color.White ? "Beyaz" : "Siyah")} {PieceName(p.Type)} yerleştiriliyor — bir kareye tıklayın"
        : "Yerleştirmek için bir taş seçin, kaldırmak için dolu bir kareye tıklayın";

    private static string PieceName(PieceType type) => type switch
    {
        PieceType.Pawn => "Piyon",
        PieceType.Knight => "At",
        PieceType.Bishop => "Fil",
        PieceType.Rook => "Kale",
        PieceType.Queen => "Vezir",
        PieceType.King => "Şah",
        _ => ""
    };

    /// <summary>Palet üzerinde bir taşı seçer/bırakır. <paramref name="code"/> ör. "wK", "bQ".</summary>
    [RelayCommand]
    private void SelectPalette(string code)
    {
        if (string.IsNullOrEmpty(code) || code.Length < 2) { ArmedPiece = null; return; }
        Color color = code[0] == 'w' ? Color.White : Color.Black;
        PieceType type = code[1] switch
        {
            'P' => PieceType.Pawn,
            'N' => PieceType.Knight,
            'B' => PieceType.Bishop,
            'R' => PieceType.Rook,
            'Q' => PieceType.Queen,
            'K' => PieceType.King,
            _ => PieceType.None
        };
        if (type == PieceType.None) { ArmedPiece = null; return; }

        var piece = new Piece(color, type);
        ArmedPiece = ArmedPiece == piece ? (Piece?)null : piece;
    }

    public void PlacePiece(int square, Piece piece)
    {
        if (square is < 0 or >= 64) return;
        // 1. ve 8. yatayda piyon olamaz.
        if (piece.Type == PieceType.Pawn && (Squares.RankOf(square) == 0 || Squares.RankOf(square) == 7))
            return;

        _squares[square] = piece;
        Refresh();
    }

    public void ClearSquare(int square)
    {
        if (square is < 0 or >= 64) return;
        _squares[square] = Piece.None;
        Refresh();
    }

    [RelayCommand]
    private void ClearBoard()
    {
        Array.Clear(_squares);
        Refresh();
    }

    [RelayCommand]
    private void LoadStandard()
    {
        LoadSquaresFromFen(Position.StartFen);
        SideToMove = Color.White;
        WhiteKingSide = true;
        WhiteQueenSide = true;
        BlackKingSide = true;
        BlackQueenSide = true;
        EnPassantSquareText = "-";
        ValidationError = "";
        Refresh();
    }

    /// <summary>Metin kutusuna yapıştırılan FEN'i ayrıştırıp editörü onunla tohumlar.</summary>
    [RelayCommand]
    private void LoadFromFenText()
    {
        try
        {
            Position pos = Position.FromFen(FenInput.Trim());
            LoadFromPosition(pos);
        }
        catch
        {
            ValidationError = "Geçersiz FEN metni.";
        }
    }

    /// <summary>Editörü verilen canlı pozisyondan tohumlar (editör açılışında çağrılır).</summary>
    public void LoadFromPosition(Position pos)
    {
        for (int sq = 0; sq < 64; sq++) _squares[sq] = pos[sq];

        SideToMove = pos.SideToMove;
        WhiteKingSide = (pos.Castling & CastlingRights.WhiteKing) != 0;
        WhiteQueenSide = (pos.Castling & CastlingRights.WhiteQueen) != 0;
        BlackKingSide = (pos.Castling & CastlingRights.BlackKing) != 0;
        BlackQueenSide = (pos.Castling & CastlingRights.BlackQueen) != 0;
        EnPassantSquareText = pos.EnPassantSquare == Squares.None ? "-" : Squares.ToAlgebraic(pos.EnPassantSquare);
        ValidationError = "";
        Refresh();
    }

    private void LoadSquaresFromFen(string fen)
    {
        Position p = Position.FromFen(fen);
        for (int sq = 0; sq < 64; sq++) _squares[sq] = p[sq];
    }

    /// <summary>Editörün mevcut durumundan bir FEN metni üretir (rok/en-passant alanları dahil).</summary>
    public string BuildFen()
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

        sb.Append(SideToMove == Color.White ? " w " : " b ");

        string castling = (WhiteKingSide ? "K" : "") + (WhiteQueenSide ? "Q" : "")
            + (BlackKingSide ? "k" : "") + (BlackQueenSide ? "q" : "");
        sb.Append(castling.Length == 0 ? "-" : castling);

        sb.Append(' ').Append(string.IsNullOrWhiteSpace(EnPassantSquareText) ? "-" : EnPassantSquareText);
        sb.Append(" 0 1");
        return sb.ToString();
    }

    /// <summary>
    /// Pozisyonu doğrular: her renkten tam bir şah, 1./8. yatayda piyon yok, rakip zaten şah
    /// çekmiyor. Rok hakları ve en passant karesi geçersizse sessizce düzeltilir (sert hata değil).
    /// </summary>
    public bool TryValidate(out string error)
    {
        error = "";

        int whiteKings = 0, blackKings = 0;
        for (int sq = 0; sq < 64; sq++)
        {
            if (_squares[sq].Type != PieceType.King) continue;
            if (_squares[sq].Color == Color.White) whiteKings++; else blackKings++;
        }
        if (whiteKings != 1 || blackKings != 1)
        {
            error = "Her renkten tam olarak bir şah olmalı.";
            return false;
        }

        for (int file = 0; file < 8; file++)
        {
            if (_squares[Squares.Of(file, 0)].Type == PieceType.Pawn ||
                _squares[Squares.Of(file, 7)].Type == PieceType.Pawn)
            {
                error = "1. veya 8. yatayda piyon olamaz.";
                return false;
            }
        }

        SanitizeCastlingRights();
        SanitizeEnPassant();

        Position candidate;
        try { candidate = Position.FromFen(BuildFen()); }
        catch { error = "Geçersiz pozisyon."; return false; }

        Color opponent = SideToMove.Opponent();
        if (candidate.IsInCheck(opponent))
        {
            error = $"{(opponent == Color.White ? "Beyaz" : "Siyah")} zaten şah çekiyor — geçersiz pozisyon.";
            return false;
        }

        Refresh();
        return true;
    }

    private void SanitizeCastlingRights()
    {
        bool WhiteKingHome() => _squares[Squares.Of(4, 0)] == new Piece(Color.White, PieceType.King);
        bool BlackKingHome() => _squares[Squares.Of(4, 7)] == new Piece(Color.Black, PieceType.King);

        if (WhiteKingSide && !(WhiteKingHome() && _squares[Squares.Of(7, 0)] == new Piece(Color.White, PieceType.Rook)))
            WhiteKingSide = false;
        if (WhiteQueenSide && !(WhiteKingHome() && _squares[Squares.Of(0, 0)] == new Piece(Color.White, PieceType.Rook)))
            WhiteQueenSide = false;
        if (BlackKingSide && !(BlackKingHome() && _squares[Squares.Of(7, 7)] == new Piece(Color.Black, PieceType.Rook)))
            BlackKingSide = false;
        if (BlackQueenSide && !(BlackKingHome() && _squares[Squares.Of(0, 7)] == new Piece(Color.Black, PieceType.Rook)))
            BlackQueenSide = false;
    }

    private void SanitizeEnPassant()
    {
        if (EnPassantSquareText == "-") return;

        int epSq = Squares.FromAlgebraic(EnPassantSquareText);
        int rank = epSq == Squares.None ? -1 : Squares.RankOf(epSq);
        bool validRank = SideToMove == Color.White ? rank == 5 : rank == 2;
        bool empty = epSq != Squares.None && _squares[epSq].IsNone;

        if (epSq == Squares.None || !empty || !validRank)
            EnPassantSquareText = "-";
    }

    private void Refresh()
    {
        // _livePosition yalnızca görüntüleme amaçlı; eksik/fazla şah gibi ara durumlarda bile
        // FromFen alan formatı bozulmadığı sürece güvenle ayrıştırılabilir.
        _livePosition = Position.FromFen(BuildFen());
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
