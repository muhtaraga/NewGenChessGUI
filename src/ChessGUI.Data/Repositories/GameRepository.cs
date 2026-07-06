using System.Text;
using Microsoft.Data.Sqlite;
using ChessGUI.Data.Entities;

namespace ChessGUI.Data.Repositories;

/// <summary>Oyun veritabanında arama ve tek oyun getirme işlemleri.</summary>
public sealed class GameRepository
{
    private readonly ChessDatabase _db;

    public GameRepository(ChessDatabase db) => _db = db;

    public long GameCount() => _db.ScalarLong("SELECT COUNT(*) FROM Games;");
    public long PlayerCount() => _db.ScalarLong("SELECT COUNT(*) FROM Players;");
    public long PositionCount() => _db.ScalarLong("SELECT COUNT(*) FROM PositionIndex;");

    /// <summary>Filtreye uyan oyun başlıklarını döndürür.</summary>
    public List<GameHeader> Search(GameQuery q)
    {
        var sql = new StringBuilder("""
            SELECT g.Id, w.Name, b.Name, IFNULL(e.Name,''), g.Result, IFNULL(g.Date,''),
                   IFNULL(g.Round,''), IFNULL(g.Eco,''), g.WhiteElo, g.BlackElo, g.PlyCount
            """);

        bool byPosition = q.PositionHash.HasValue;
        if (byPosition)
            sql.Append(", pi.Ply");

        sql.Append("""
             FROM Games g
             JOIN Players w ON w.Id = g.WhiteId
             JOIN Players b ON b.Id = g.BlackId
             LEFT JOIN Events e ON e.Id = g.EventId
            """);

        if (byPosition)
            sql.Append(" JOIN PositionIndex pi ON pi.GameId = g.Id AND pi.Hash = @poshash");

        using var cmd = _db.Connection.CreateCommand();
        var where = new List<string>();

        void Add(string clause, string param, object value)
        {
            where.Add(clause);
            cmd.Parameters.AddWithValue(param, value);
        }

        if (byPosition)
            cmd.Parameters.AddWithValue("@poshash", unchecked((long)q.PositionHash!.Value));
        if (!string.IsNullOrWhiteSpace(q.Player))
        {
            where.Add("(w.Name LIKE @player OR b.Name LIKE @player)");
            cmd.Parameters.AddWithValue("@player", Like(q.Player));
        }
        if (!string.IsNullOrWhiteSpace(q.White)) Add("w.Name LIKE @white", "@white", Like(q.White));
        if (!string.IsNullOrWhiteSpace(q.Black)) Add("b.Name LIKE @black", "@black", Like(q.Black));
        if (!string.IsNullOrWhiteSpace(q.Event)) Add("e.Name LIKE @event", "@event", Like(q.Event));
        if (!string.IsNullOrWhiteSpace(q.Eco)) Add("g.Eco LIKE @eco", "@eco", q.Eco.Trim() + "%");
        if (!string.IsNullOrWhiteSpace(q.Result)) Add("g.Result = @result", "@result", q.Result);
        if (!string.IsNullOrWhiteSpace(q.DateFrom)) Add("g.Date >= @dfrom", "@dfrom", q.DateFrom);
        if (!string.IsNullOrWhiteSpace(q.DateTo)) Add("g.Date <= @dto", "@dto", q.DateTo);
        if (q.MinElo is int minElo)
            Add("(g.WhiteElo >= @minelo OR g.BlackElo >= @minelo)", "@minelo", minElo);

        if (where.Count > 0)
            sql.Append(" WHERE ").Append(string.Join(" AND ", where));

        sql.Append(" ORDER BY g.Date DESC, g.Id DESC LIMIT @limit OFFSET @offset;");
        cmd.Parameters.AddWithValue("@limit", q.Limit);
        cmd.Parameters.AddWithValue("@offset", q.Offset);
        cmd.CommandText = sql.ToString();

        var results = new List<GameHeader>();
        using SqliteDataReader r = cmd.ExecuteReader();
        while (r.Read())
        {
            results.Add(new GameHeader
            {
                Id = r.GetInt64(0),
                White = r.GetString(1),
                Black = r.GetString(2),
                Event = r.GetString(3),
                Result = r.GetString(4),
                Date = DisplayDate(r.GetString(5)),
                Round = r.GetString(6),
                Eco = r.GetString(7),
                WhiteElo = r.IsDBNull(8) ? null : r.GetInt32(8),
                BlackElo = r.IsDBNull(9) ? null : r.GetInt32(9),
                PlyCount = r.GetInt32(10),
                MatchPly = byPosition && !r.IsDBNull(11) ? r.GetInt32(11) : null
            });
        }
        return results;
    }

    /// <summary>Bir oyunun ham PGN metnini getirir (ağaç kurmak için).</summary>
    public string? LoadPgn(long id)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT Pgn FROM Games WHERE Id = @id;";
        cmd.Parameters.AddWithValue("@id", id);
        return cmd.ExecuteScalar() as string;
    }

    private static string Like(string s) => "%" + s.Trim() + "%";

    /// <summary>"YYYY-MM-DD" → görüntüleme için "YYYY.MM.DD"; sıfır alanlar gizlenir.</summary>
    private static string DisplayDate(string normalized)
    {
        if (normalized.Length < 4) return "";
        string[] p = normalized.Split('-');
        string y = p[0] == "0000" ? "????" : p[0];
        if (p.Length < 2 || p[1] == "00") return y;
        if (p.Length < 3 || p[2] == "00") return $"{y}.{p[1]}";
        return $"{y}.{p[1]}.{p[2]}";
    }
}
