using Microsoft.Data.Sqlite;
using ChessGUI.Core.Notation;
using ChessGUI.Data.Import;

namespace ChessGUI.Data;

/// <summary>
/// SQLite oyun veritabanı: bağlantı açma, şema kurulumu ve toplu içe aktarma için hız ayarları
/// (PRAGMA). Şema — Players/Events (normalize adlar), Games (özet + hamle metni) ve pozisyon
/// araması için PositionIndex(Hash, GameId, Ply). Zobrist anahtarı imzalı 64-bit olarak saklanır.
/// </summary>
public sealed class ChessDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public string Path { get; }
    public SqliteConnection Connection => _connection;

    private ChessDatabase(SqliteConnection connection, string path)
    {
        _connection = connection;
        Path = path;
    }

    /// <summary>Verilen dosyayı açar (yoksa oluşturur), şemayı kurar ve WAL modunu etkinleştirir.</summary>
    public static ChessDatabase Open(string path)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private
        };
        var connection = new SqliteConnection(builder.ConnectionString);
        connection.Open();

        var db = new ChessDatabase(connection, path);
        db.Exec("PRAGMA journal_mode=WAL;");
        db.Exec("PRAGMA synchronous=NORMAL;");
        db.Exec("PRAGMA foreign_keys=ON;");
        db.Exec("PRAGMA temp_store=MEMORY;");
        db.CreateSchema();
        return db;
    }

    /// <summary>Yalnızca okuma amaçlı geçici bellek veritabanı (testler için).</summary>
    public static ChessDatabase OpenInMemory()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var db = new ChessDatabase(connection, ":memory:");
        db.CreateSchema();
        return db;
    }

    private void CreateSchema()
    {
        Exec("""
            CREATE TABLE IF NOT EXISTS Players (
                Id   INTEGER PRIMARY KEY,
                Name TEXT NOT NULL UNIQUE
            );
            CREATE TABLE IF NOT EXISTS Events (
                Id   INTEGER PRIMARY KEY,
                Name TEXT NOT NULL UNIQUE
            );
            CREATE TABLE IF NOT EXISTS Games (
                Id       INTEGER PRIMARY KEY,
                WhiteId  INTEGER NOT NULL REFERENCES Players(Id),
                BlackId  INTEGER NOT NULL REFERENCES Players(Id),
                EventId  INTEGER REFERENCES Events(Id),
                Result   TEXT,
                Date     TEXT,
                Round    TEXT,
                Eco      TEXT,
                WhiteElo INTEGER,
                BlackElo INTEGER,
                PlyCount INTEGER NOT NULL DEFAULT 0,
                StartFen TEXT,
                Pgn      TEXT NOT NULL,
                MovesHash INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS ix_games_white ON Games(WhiteId);
            CREATE INDEX IF NOT EXISTS ix_games_black ON Games(BlackId);
            CREATE INDEX IF NOT EXISTS ix_games_event ON Games(EventId);
            CREATE INDEX IF NOT EXISTS ix_games_eco   ON Games(Eco);
            CREATE INDEX IF NOT EXISTS ix_games_date  ON Games(Date);
            CREATE TABLE IF NOT EXISTS PositionIndex (
                Hash     INTEGER NOT NULL,
                GameId   INTEGER NOT NULL REFERENCES Games(Id),
                Ply      INTEGER NOT NULL,
                NextMove TEXT
            );
            CREATE INDEX IF NOT EXISTS ix_posindex_hash ON PositionIndex(Hash);
            """);

        // Aşama 6'da oluşturulmuş eski veritabanlarını (NextMove sütunu olmadan) geriye dönük yükselt.
        EnsureColumn("PositionIndex", "NextMove", "TEXT");

        // MovesHash eklenmeden önce oluşturulmuş eski veritabanlarını yükselt ve mevcut oyunlar
        // için hash'i geriye dönük hesapla (aksi halde yükseltmeden önce eklenmiş oyunlar
        // yinelenen-oyun kontrolünde hiç yakalanmaz). İndeks, sütun kesin var olduktan sonra kurulur —
        // aksi halde eski veritabanlarında "no such column" hatası verir.
        bool addedMovesHash = EnsureColumn("Games", "MovesHash", "INTEGER NOT NULL DEFAULT 0");
        Exec("CREATE INDEX IF NOT EXISTS ix_games_moveshash ON Games(MovesHash);");
        if (addedMovesHash) BackfillMovesHash();
    }

    private bool EnsureColumn(string table, string column, string sqlType)
    {
        using (var check = _connection.CreateCommand())
        {
            check.CommandText = $"PRAGMA table_info({table});";
            using SqliteDataReader r = check.ExecuteReader();
            while (r.Read())
                if (string.Equals(r.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                    return false;
        }
        Exec($"ALTER TABLE {table} ADD COLUMN {column} {sqlType};");
        return true;
    }

    /// <summary>Var olan tüm oyunların ham PGN'ini yeniden ayrıştırıp <c>MovesHash</c>'i doldurur.</summary>
    private void BackfillMovesHash()
    {
        var updates = new List<(long Id, long Hash)>();
        using (var select = _connection.CreateCommand())
        {
            select.CommandText = "SELECT Id, Pgn FROM Games;";
            using SqliteDataReader r = select.ExecuteReader();
            while (r.Read())
            {
                long id = r.GetInt64(0);
                string pgn = r.GetString(1);
                long hash = 0;
                try { hash = MovesHasher.Compute(Pgn.Parse(pgn)); } catch { /* bozuk PGN — hash 0 kalır */ }
                updates.Add((id, hash));
            }
        }
        if (updates.Count == 0) return;

        using SqliteTransaction tx = _connection.BeginTransaction();
        using (var update = _connection.CreateCommand())
        {
            update.Transaction = tx;
            update.CommandText = "UPDATE Games SET MovesHash = @h WHERE Id = @id;";
            update.Parameters.Add("@h", SqliteType.Integer);
            update.Parameters.Add("@id", SqliteType.Integer);
            foreach ((long id, long hash) in updates)
            {
                update.Parameters["@h"].Value = hash;
                update.Parameters["@id"].Value = id;
                update.ExecuteNonQuery();
            }
        }
        tx.Commit();
    }

    /// <summary>İçe aktarma sırasında dayanıklılığı geçici olarak düşürerek hızlandırır.</summary>
    public void BeginBulkLoad()
    {
        Exec("PRAGMA synchronous=OFF;");
        Exec("PRAGMA journal_mode=MEMORY;");
    }

    /// <summary>Toplu yükleme bittikten sonra güvenli ayarları geri getirir ve alanı toparlar.</summary>
    public void EndBulkLoad()
    {
        Exec("PRAGMA synchronous=NORMAL;");
        Exec("PRAGMA journal_mode=WAL;");
    }

    public long ScalarLong(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        object? result = cmd.ExecuteScalar();
        return result is null or DBNull ? 0 : Convert.ToInt64(result);
    }

    public void Exec(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _connection.Dispose();
        SqliteConnection.ClearAllPools();
    }
}
