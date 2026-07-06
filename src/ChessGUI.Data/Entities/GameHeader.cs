namespace ChessGUI.Data.Entities;

/// <summary>
/// Bir oyunun veritabanındaki özet satırı (arama sonucu listesi için). Hamle metni ayrıca,
/// oyun açılırken <see cref="Repositories.GameRepository.LoadPgn"/> ile getirilir.
/// </summary>
public sealed class GameHeader
{
    public long Id { get; init; }
    public string White { get; init; } = "?";
    public string Black { get; init; } = "?";
    public string Event { get; init; } = "";
    public string Result { get; init; } = "*";
    public string Date { get; init; } = "";
    public string Round { get; init; } = "";
    public string Eco { get; init; } = "";
    public int? WhiteElo { get; init; }
    public int? BlackElo { get; init; }
    public int PlyCount { get; init; }

    /// <summary>Pozisyon araması sonucunda: aranan pozisyonun bu oyundaki hamle sırası (varsa).</summary>
    public int? MatchPly { get; init; }

    public string MovesText => $"{PlyCount / 2 + PlyCount % 2} hamle";
    public string EloText => WhiteElo is int w && BlackElo is int b ? $"{w}–{b}" : "";
}
