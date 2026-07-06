namespace ChessGUI.Data;

/// <summary>
/// Oyun arama filtresi. Tüm alanlar isteğe bağlıdır; dolu olanlar VE (AND) mantığıyla birleşir.
/// Oyuncu adları "içeren" (LIKE) eşleşmesi kullanır; ECO ön ek eşleşmesidir.
/// </summary>
public sealed class GameQuery
{
    /// <summary>Herhangi bir renkte oynayan oyuncu adı (beyaz VEYA siyah).</summary>
    public string? Player { get; set; }

    /// <summary>Yalnızca beyaz oyuncu adı.</summary>
    public string? White { get; set; }

    /// <summary>Yalnızca siyah oyuncu adı.</summary>
    public string? Black { get; set; }

    public string? Event { get; set; }

    /// <summary>ECO kodu ön eki (ör. "B" veya "B90").</summary>
    public string? Eco { get; set; }

    /// <summary>Sonuç: "1-0", "0-1", "1/2-1/2".</summary>
    public string? Result { get; set; }

    /// <summary>Normalize tarih alt sınırı ("YYYY-MM-DD").</summary>
    public string? DateFrom { get; set; }
    public string? DateTo { get; set; }

    public int? MinElo { get; set; }

    /// <summary>Verilirse yalnızca bu Zobrist pozisyonunu içeren oyunlar döner.</summary>
    public ulong? PositionHash { get; set; }

    public int Limit { get; set; } = 500;
    public int Offset { get; set; }
}
