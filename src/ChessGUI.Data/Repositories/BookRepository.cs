using Microsoft.Data.Sqlite;
using ChessGUI.Data.Entities;

namespace ChessGUI.Data.Repositories;

/// <summary>
/// Kendi veritabanımızdan türetilen "açılış kitabı": verilen bir pozisyondan (Zobrist anahtarı)
/// sonra içe aktarılmış oyunlarda oynanan hamleleri, oyun sayısı ve sonuç dağılımıyla getirir.
/// Dış bir .bin dosyasına bağımlı değildir — istatistikler tamamen içe aktarılan PGN'lerden gelir.
/// </summary>
public sealed class BookRepository
{
    private readonly ChessDatabase _db;

    public BookRepository(ChessDatabase db) => _db = db;

    /// <summary>Verilen pozisyondan sonra oynanan hamleleri, oyun sayısına göre azalan sırada döndürür.</summary>
    public List<BookMoveStat> GetMoves(ulong positionHash)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT pi.NextMove,
                   COUNT(*) AS Total,
                   SUM(CASE WHEN g.Result = '1-0' THEN 1 ELSE 0 END),
                   SUM(CASE WHEN g.Result = '1/2-1/2' THEN 1 ELSE 0 END),
                   SUM(CASE WHEN g.Result = '0-1' THEN 1 ELSE 0 END)
            FROM PositionIndex pi
            JOIN Games g ON g.Id = pi.GameId
            WHERE pi.Hash = @hash AND pi.NextMove IS NOT NULL
            GROUP BY pi.NextMove
            ORDER BY Total DESC;
            """;
        cmd.Parameters.AddWithValue("@hash", unchecked((long)positionHash));

        var results = new List<BookMoveStat>();
        using SqliteDataReader r = cmd.ExecuteReader();
        while (r.Read())
            results.Add(new BookMoveStat(r.GetString(0), r.GetInt32(1), r.GetInt32(2), r.GetInt32(3), r.GetInt32(4)));
        return results;
    }
}
