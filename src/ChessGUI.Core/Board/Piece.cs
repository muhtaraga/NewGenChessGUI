namespace ChessGUI.Core.Board;

/// <summary>Taşın rengi.</summary>
public enum Color : byte
{
    White = 0,
    Black = 1
}

/// <summary>Taş türü. <see cref="None"/> boş kareyi temsil eder.</summary>
public enum PieceType : byte
{
    None = 0,
    Pawn = 1,
    Knight = 2,
    Bishop = 3,
    Rook = 4,
    Queen = 5,
    King = 6
}

/// <summary>
/// Bir kareyi kaplayan taş. <see cref="Type"/> = <see cref="PieceType.None"/> ise kare boştur.
/// Değer tipidir; boş kare <c>default</c> ile temsil edilir.
/// </summary>
public readonly record struct Piece(Color Color, PieceType Type)
{
    public static readonly Piece None = new(Color.White, PieceType.None);

    public bool IsNone => Type == PieceType.None;
    public bool IsWhite => Type != PieceType.None && Color == Color.White;
    public bool IsBlack => Type != PieceType.None && Color == Color.Black;

    /// <summary>FEN karakteri: beyaz büyük, siyah küçük harf (örn. 'N', 'q').</summary>
    public char ToFenChar()
    {
        char c = Type switch
        {
            PieceType.Pawn => 'p',
            PieceType.Knight => 'n',
            PieceType.Bishop => 'b',
            PieceType.Rook => 'r',
            PieceType.Queen => 'q',
            PieceType.King => 'k',
            _ => ' '
        };
        return Color == Color.White ? char.ToUpperInvariant(c) : c;
    }

    /// <summary>FEN karakterinden taş üretir. Geçersizse <see cref="None"/>.</summary>
    public static Piece FromFenChar(char c)
    {
        var color = char.IsUpper(c) ? Color.White : Color.Black;
        var type = char.ToLowerInvariant(c) switch
        {
            'p' => PieceType.Pawn,
            'n' => PieceType.Knight,
            'b' => PieceType.Bishop,
            'r' => PieceType.Rook,
            'q' => PieceType.Queen,
            'k' => PieceType.King,
            _ => PieceType.None
        };
        return type == PieceType.None ? None : new Piece(color, type);
    }
}

public static class ColorExtensions
{
    public static Color Opponent(this Color color) => color == Color.White ? Color.Black : Color.White;
}
