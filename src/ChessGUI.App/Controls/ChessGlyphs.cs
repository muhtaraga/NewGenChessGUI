using ChessGUI.Core.Board;

namespace ChessGUI.App.Controls;

/// <summary>
/// Satranç taşlarını Unicode gliflerinden çizmek için eşlemeler. İki katman kullanılır:
/// dolu "gövde" glifi (taş rengiyle doldurulur) ve içi boş "kenar" glifi (üstüne zıt renkle
/// çizilerek net bir dış hat verir). Böylece harici görsel dosyasına gerek kalmaz ve taşlar
/// hem açık hem koyu karelerde belirgin görünür.
/// </summary>
public static class ChessGlyphs
{
    // Dolu (siyah) glifler — gövde olarak kullanılır.
    public static string Body(PieceType type) => type switch
    {
        PieceType.King => "♚",
        PieceType.Queen => "♛",
        PieceType.Rook => "♜",
        PieceType.Bishop => "♝",
        PieceType.Knight => "♞",
        PieceType.Pawn => "♟",
        _ => ""
    };

    // İçi boş (beyaz) glifler — kenar/dış hat olarak kullanılır (gövdeyle aynı metriklerde hizalanır).
    public static string Edge(PieceType type) => type switch
    {
        PieceType.King => "♔",
        PieceType.Queen => "♕",
        PieceType.Rook => "♖",
        PieceType.Bishop => "♗",
        PieceType.Knight => "♘",
        PieceType.Pawn => "♙",
        _ => ""
    };
}
